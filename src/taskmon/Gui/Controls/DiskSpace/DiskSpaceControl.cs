using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.InputBox;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Controls.Metre;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.DiskSpace;

namespace Task.Monitor.Gui.Controls.DiskSpace;

// An on-demand folder-size scan: a live treemap heat map of the scan root's folders in the top
// half, and the largest individual files found so far in the bottom half. Scanning is started and
// cancelled from here ('s' / 'c') rather than run automatically, since a full scan is expensive.
public sealed partial class DiskSpaceControl : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private readonly DiskSpaceHeatMapControl heatMap;
    private readonly MetreControl progressMetre;
    private readonly ListView filesView;

    // Not added to Controls, the same way Screen keeps its own message/input boxes out of its
    // Controls collection - it is only ever shown modally, positioned and drawn explicitly rather
    // than taking part in the normal child-control layout pass.
    private readonly InputBox scanPathInputBox;

    private DiskSpaceInfo? diskSpace;
    private string lastFileListSignature = string.Empty;

    private const int FileColumnMinWidth = 16;
    private const int FileCountRowHeight = 1;
    private const int SizeColumnWidth = 12;
    
    private int fileCountRowX;
    private int fileCountRowY;
    private int fileCountRowWidth;    

    public DiskSpaceControl(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;

        heatMap = new DiskSpaceHeatMapControl(terminal, appConfig) {
            Visible = true
        };

        // A single-series metre used as a plain progress bar: how many of the scan root's
        // immediate child folders have been fully walked, out of the total found so far.
        progressMetre = new MetreControl(terminal) {
            Visible = true,
            TabStop = false,
            Border = false,
            ShowLegend = true,
            Rows = 1
        };

        filesView = new ListView(terminal) {
            EnableScroll = true,
            EnableRowSelect = true,
            ShowColumnHeaders = true,
            ShowBorder = true,
            TabStop = true,
            TabIndex = 2,
            Visible = true,
            EmptyListViewText = "No files scanned yet - press s to start a scan.",
            HeaderText = "LARGEST FILES"
        };

        filesView.ColumnHeaders
            .Add(new ListViewColumnHeader("FILE"))
            .Add(new ListViewColumnHeader("SIZE"))
            .Add(new ListViewColumnHeader("PATH"));

        scanPathInputBox = new InputBox(terminal) {
            Width = 48,
            Height = 1,
            Visible = false
        };

        Controls.Add(heatMap);
        Controls.Add(progressMetre);
        Controls.Add(filesView);
    }

    // Wired to the controller event in OnLoad; tests call it directly.
    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.DiskSpace is null) {
            return;
        }

        diskSpace = snapshot.DiskSpace;
        Draw();
    }

    private void DrawFileCountRow(DiskSpaceSpecs? specs)
    {
        string text = specs?.State switch {
            null => string.Empty,
            DiskSpaceScanState.Idle => string.Empty,
            DiskSpaceScanState.Scanning or DiskSpaceScanState.Cancelling => $"Processing {specs.FilesScanned:N0} files",
            _ => $"Processed {specs.FilesScanned:N0} files"
        };

        int charsToTake = text.TruncateToTerminalWidth(fileCountRowWidth, out int actualWidth);
        string padded = text[..charsToTake] + new string(' ', fileCountRowWidth - actualWidth);
        
        Terminal.SetCursorPosition(fileCountRowX, fileCountRowY);
        Terminal.BackgroundColor = BackgroundColour;
        Terminal.ForegroundColor = ForegroundColour;
        Terminal.Write(padded);
    }
    
    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();

            if (diskSpace is { } current) {
                heatMap.Sample(current.Specs);
                EnsureFileRows(current.Specs);
            }

            heatMap.Draw();
            UpdateProgressMetre(diskSpace?.Specs);
            DrawFileCountRow(diskSpace?.Specs);
            filesView.Draw();

            if (scanPathInputBox.Visible) {
                scanPathInputBox.Draw();
            }
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    // Rebuilds the row list only when the scan's progress counters actually changed - cheap while
    // a scan is running (they change on every throttled publish) and free once it finishes.
    private void EnsureFileRows(DiskSpaceSpecs specs)
    {
        string signature = $"{specs.State}|{specs.FilesScanned}|{specs.TotalBytesScanned}";

        if (signature == lastFileListSignature) {
            return;
        }

        lastFileListSignature = signature;
        RebuildFileRows(specs.TopFiles);
    }

    // RootLevelFoldersTotal settles almost immediately once a scan starts (the root's own
    // contents are enumerated in one pass right at the start of the walk), so this is a
    // meaningful percentage for nearly the whole scan, not just at the very end.
    private void UpdateProgressMetre(DiskSpaceSpecs? specs)
    {
        double ratio = specs switch {
            null => 0.0,
            { RootLevelFoldersTotal: > 0 } => (double)specs.RootLevelFoldersCompleted / specs.RootLevelFoldersTotal,
            { State: DiskSpaceScanState.Completed } => 1.0,
            _ => 0.0
        };

        progressMetre.SetValue(0, ratio);
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        if (scanPathInputBox.Visible) {
            OnScanPathInputBoxKeyPressed(keyInfo, ref handled);
            return;
        }

        switch (keyInfo.Key) {
            case ConsoleKey.S:
                ShowScanPathPrompt();
                handled = true;
                return;

            case ConsoleKey.C:
                TryCancelScan();
                handled = true;
                return;
        }

        filesView.KeyPressed(keyInfo, ref handled);
    }

    private void ShowScanPathPrompt()
    {
        string defaultRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? string.Empty;

        Control.RedrawEnabled = false;

        scanPathInputBox.X = X + 2;
        scanPathInputBox.Y = Y + Math.Max(0, Height / 2);
        scanPathInputBox.Width = Math.Clamp(Width - 4, 20, 60);
        scanPathInputBox.Title = "Scan path:";
        scanPathInputBox.Visible = true;
        scanPathInputBox.SetText(defaultRoot);
        scanPathInputBox.ShowInputBox();
    }

    private void OnScanPathInputBoxKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        scanPathInputBox.KeyPressed(keyInfo, ref handled);

        if (scanPathInputBox.Result == InputBoxResult.None) {
            return;
        }

        string path = scanPathInputBox.Text;
        InputBoxResult result = scanPathInputBox.Result;

        Control.RedrawEnabled = true;
        scanPathInputBox.Visible = false;

        if (result == InputBoxResult.Enter && !string.IsNullOrWhiteSpace(path)) {
            TryStartScan(path.Trim());
        }

        Draw();
    }

    private void TryStartScan(string path)
    {
        try {
            serviceController.GetService<DiskSpaceService>().StartScan(path);
        }
        catch (InvalidOperationException) {
            // No DiskSpaceService registered (e.g. under test) - nothing to start.
        }
    }

    private void TryCancelScan()
    {
        try {
            serviceController.GetService<DiskSpaceService>().CancelScan();
        }
        catch (InvalidOperationException) {
            // No DiskSpaceService registered (e.g. under test) - nothing to cancel.
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;

        heatMap.BackgroundColour = appConfig.DefaultTheme.Background;
        heatMap.ForegroundColour = appConfig.DefaultTheme.Foreground;

        progressMetre.BackgroundColour = appConfig.DefaultTheme.Background;
        progressMetre.ForegroundColour = appConfig.DefaultTheme.Foreground;
        progressMetre.MetreStyle = appConfig.MetreStyle;
        progressMetre.AddSeries("Root Folders", appConfig.DefaultTheme.RangeLowBackground);

        filesView.BackgroundColour = appConfig.DefaultTheme.Background;
        filesView.ForegroundColour = appConfig.DefaultTheme.Foreground;
        filesView.BorderColour = appConfig.DefaultTheme.ListViewBorder;
        filesView.HeaderBackgroundColour = appConfig.DefaultTheme.HeaderBackground;
        filesView.HeaderForegroundColour = appConfig.DefaultTheme.HeaderForeground;
        filesView.BackgroundHighlightColour = appConfig.DefaultTheme.BackgroundHighlight;
        filesView.ForegroundHighlightColour = appConfig.DefaultTheme.ForegroundHighlight;

        foreach (ListViewColumnHeader columnHeader in filesView.ColumnHeaders) {
            columnHeader.BackgroundColour = appConfig.DefaultTheme.HeaderBackground;
            columnHeader.ForegroundColour = appConfig.DefaultTheme.HeaderForeground;
        }

        scanPathInputBox.BackgroundColour = appConfig.DefaultTheme.HeaderBackground;
        scanPathInputBox.ForegroundColour = appConfig.DefaultTheme.HeaderForeground;
        scanPathInputBox.Load();

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnResize()
    {
        int progressMetreHeight = progressMetre.RequiredHeight;
        int remainingHeight = Math.Max(0, Height - progressMetreHeight - FileCountRowHeight);
        int heatMapHeight = Math.Max(3, remainingHeight / 2);
        int filesViewHeight = Math.Max(1, remainingHeight - heatMapHeight);

        heatMap.X = X;
        heatMap.Y = Y;
        heatMap.Width = Width;
        heatMap.Height = heatMapHeight;
        heatMap.Resize();

        progressMetre.X = X;
        progressMetre.Y = Y + heatMapHeight;
        progressMetre.Width = Width - 1;
        progressMetre.Height = progressMetreHeight;
        progressMetre.Resize();
        
        fileCountRowX = X;
        fileCountRowY = Y + heatMapHeight + progressMetreHeight;
        fileCountRowWidth = Width;

        filesView.X = X;
        filesView.Y = fileCountRowY + FileCountRowHeight;
        filesView.Width = Width;
        filesView.Height = filesViewHeight;

        int fileColumnWidth = Math.Max(FileColumnMinWidth, Width / 4);
        int fixedWidth = fileColumnWidth + SizeColumnWidth;

        filesView.ColumnHeaders[0].Width = fileColumnWidth;
        filesView.ColumnHeaders[1].Width = SizeColumnWidth;
        filesView.ColumnHeaders[2].Width = Math.Max(1, Width - fixedWidth - 3);

        base.OnResize();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        progressMetre.ClearSeries();
        filesView.Items.Clear();
        lastFileListSignature = string.Empty;
        scanPathInputBox.Unload();

        base.OnUnload();
    }
}
