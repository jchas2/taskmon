using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.DriveInputBox;
using Task.Monitor.System.Controls.InputBox;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Controls.Metre;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.DiskSpace;

namespace Task.Monitor.Gui.Controls.DiskSpace;

public sealed partial class DiskSpaceControl : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private readonly DiskSpaceHeatMapControl heatMap;
    private readonly MetreControl progressMetre;
    private readonly ListView filesView;

    private MetreControlSeries? rootFoldersSeries;

    private readonly DriveInputBox driveInputBox;
    private readonly InputBox scanPathInputBox;

    private DiskSpaceInfo? diskSpace;
    private string lastFileListSignature = string.Empty;

    private const int FileColumnMinWidth = 16;
    private const int FileCountRowHeight = 1;
    private const int SizeColumnWidth = 12;
    private const int PreferredVisibleDriveRows = 5;
    
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

        driveInputBox = new DriveInputBox(terminal) {
            Visible = false
        };

        scanPathInputBox = new InputBox(terminal) {
            Width = 48,
            Height = 1,
            Visible = false
        };

        Controls.Add(heatMap);
        Controls.Add(progressMetre);
        Controls.Add(filesView);
    }

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.DiskSpace is not { } latest) {
            return;
        }

        bool changed = !ReferenceEquals(latest.Specs, diskSpace?.Specs);
        diskSpace = latest;

        if (changed || heatMap.IsFading) {
            Draw();
        }
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
    
    protected override void OnGotFocus() => filesView.SetFocus();

    public override bool HasFocus => GetFocusedControl?.HasFocus ?? false;

    protected override void OnDraw()
    {
        if (diskSpace is { } current) {
            heatMap.Sample(current.Specs);
            EnsureFileRows(current.Specs);
        }

        heatMap.Draw();
        UpdateProgressMetre(diskSpace?.Specs);
        DrawFileCountRow(diskSpace?.Specs);
        filesView.Draw();

        if (driveInputBox.Visible) {
            driveInputBox.Draw();
        }
        else if (scanPathInputBox.Visible) {
            scanPathInputBox.Draw();
        }
    }

    private void EnsureFileRows(DiskSpaceSpecs specs)
    {
        string signature = $"{specs.State}|{specs.FilesScanned}|{specs.TotalBytesScanned}";

        if (signature == lastFileListSignature) {
            return;
        }

        lastFileListSignature = signature;
        RebuildFileRows(specs.TopFiles);
    }

    private void UpdateProgressMetre(DiskSpaceSpecs? specs)
    {
        double ratio = specs switch {
            null => 0.0,
            { RootLevelFoldersTotal: > 0 } => (double)specs.RootLevelFoldersCompleted / specs.RootLevelFoldersTotal,
            { State: DiskSpaceScanState.Completed } => 1.0,
            _ => 0.0
        };

        if (rootFoldersSeries is not null) {
            rootFoldersSeries.Label = string.IsNullOrEmpty(specs?.RootPath) 
                ? "Root Folders" 
                : specs.RootPath;
        }

        progressMetre.SetValue(0, ratio);
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        if (driveInputBox.Visible) {
            OnDriveInputBoxKeyPressed(keyInfo, ref handled);
            return;
        }

        if (scanPathInputBox.Visible) {
            OnScanPathInputBoxKeyPressed(keyInfo, ref handled);
            return;
        }

        switch (keyInfo.Key) {
            case ConsoleKey.S:
                ShowDriveSelectionPrompt();
                handled = true;
                return;

            case ConsoleKey.C:
                TryCancelScan();
                handled = true;
                return;
        }

        filesView.KeyPressed(keyInfo, ref handled);
    }

    private void ShowDriveSelectionPrompt()
    {
        Control.RedrawEnabled = false;

        IReadOnlyList<string> candidates = ScanRootProvider.GetCandidates();

        driveInputBox.Width = Math.Clamp(Width - 4, 30, 60);
        driveInputBox.Height = Math.Clamp(DriveInputBox.GetPreferredHeight(PreferredVisibleDriveRows), 8, Math.Max(8, Height - 2));
        driveInputBox.X = X + Math.Max(0, (Width - driveInputBox.Width) / 2);
        driveInputBox.Y = Y + Math.Max(0, (Height - driveInputBox.Height) / 2);
        driveInputBox.Title = "Select a drive";
        driveInputBox.SetCandidates(candidates);
        driveInputBox.Visible = true;
        driveInputBox.ShowDriveInputBox();
    }

    private void OnDriveInputBoxKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        driveInputBox.KeyPressed(keyInfo, ref handled);

        if (driveInputBox.Result == DriveInputBoxResult.None) {
            return;
        }

        DriveInputBoxResult result = driveInputBox.Result;
        bool customPathRequested = driveInputBox.CustomPathRequested;
        string? selectedPath = driveInputBox.SelectedPath;

        driveInputBox.Visible = false;

        if (result == DriveInputBoxResult.Cancel) {
            Control.RedrawEnabled = true;
            Draw();
            return;
        }

        if (customPathRequested) {
            ShowScanPathPrompt();
            return;
        }

        Control.RedrawEnabled = true;

        if (!string.IsNullOrWhiteSpace(selectedPath)) {
            TryStartScan(selectedPath);
        }

        Draw();
    }

    private void ShowScanPathPrompt()
    {
        string defaultRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? string.Empty;
        Control.RedrawEnabled = false;

        // Overlay so it looks like an in-place edit to the user.
        scanPathInputBox.X = driveInputBox.ListX;
        scanPathInputBox.Y = driveInputBox.ListY;
        scanPathInputBox.Width = driveInputBox.ListWidth;
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
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;

        heatMap.BackgroundColour = appConfig.Theme.Background;
        heatMap.ForegroundColour = appConfig.Theme.Foreground;

        progressMetre.BackgroundColour = appConfig.Theme.MetreBackground;
        progressMetre.ForegroundColour = appConfig.Theme.MetreForeground;
        progressMetre.BorderForegroundColour = appConfig.Theme.MetreBorderForeground;
        progressMetre.BorderBackgroundColour = appConfig.Theme.MetreBorderBackground;
        progressMetre.MetreStyle = appConfig.MetreStyle;
        rootFoldersSeries = progressMetre.AddSeries("Root Folders", appConfig.Theme.RangeLowBackground);

        filesView.BackgroundColour = appConfig.Theme.ListViewBackground;
        filesView.ForegroundColour = appConfig.Theme.ListViewForeground;
        filesView.BorderForegroundColour = appConfig.Theme.ListViewBorderForeground;
        filesView.BorderBackgroundColour = appConfig.Theme.ListViewBorderBackground;
        filesView.HeaderBackgroundColour = appConfig.Theme.HeaderBackground;
        filesView.HeaderForegroundColour = appConfig.Theme.HeaderForeground;
        filesView.BackgroundHighlightColour = appConfig.Theme.BackgroundHighlight;
        filesView.ForegroundHighlightColour = appConfig.Theme.ForegroundHighlight;
        filesView.BackgroundHighlightInactiveColour = appConfig.Theme.BackgroundHighlightInactive;
        filesView.ForegroundHighlightInactiveColour = appConfig.Theme.ForegroundHighlightInactive;

        foreach (ListViewColumnHeader columnHeader in filesView.ColumnHeaders) {
            columnHeader.BackgroundColour = appConfig.Theme.HeaderBackground;
            columnHeader.ForegroundColour = appConfig.Theme.HeaderForeground;
        }

        driveInputBox.DialogBackgroundColour = appConfig.Theme.HeaderBackground;
        driveInputBox.DialogBorderColour = appConfig.Theme.HeaderForeground;
        driveInputBox.DialogButtonBackgroundColour = appConfig.Theme.BackgroundHighlight;
        driveInputBox.DialogButtonForegroundColour = appConfig.Theme.ForegroundHighlight;
        driveInputBox.DialogForegroundColour = appConfig.Theme.HeaderForeground;
        driveInputBox.ListBackgroundHighlightColour = appConfig.Theme.BackgroundHighlight;
        driveInputBox.ListForegroundHighlightColour = appConfig.Theme.ForegroundHighlight;
        driveInputBox.ListBackgroundHighlightInactiveColour = appConfig.Theme.BackgroundHighlightInactive;
        driveInputBox.ListForegroundHighlightInactiveColour = appConfig.Theme.ForegroundHighlightInactive;
        driveInputBox.Load();

        scanPathInputBox.BackgroundColour = appConfig.Theme.HeaderBackground;
        scanPathInputBox.ForegroundColour = appConfig.Theme.HeaderForeground;
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
        rootFoldersSeries = null;
        filesView.Items.Clear();
        lastFileListSignature = string.Empty;
        driveInputBox.Unload();
        scanPathInputBox.Unload();

        base.OnUnload();
    }
}
