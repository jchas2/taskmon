using System.Drawing;
using System.Globalization;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.Gui.Controls.Summary2;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.Resources;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.MessageBox;
using Task.Monitor.System.Controls.PickerBox;
using Task.Monitor.System.Controls.TextInputDialog;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Services;
using MessageBoxControl = Task.Monitor.System.Controls.MessageBox.MessageBox;
using PickerBoxControl = Task.Monitor.System.Controls.PickerBox.PickerBox;
using TextInputDialogControl = Task.Monitor.System.Controls.TextInputDialog.TextInputDialog;

namespace Task.Monitor.Gui;

// A live WYSIWYG editor for the SummaryLayout control.
public sealed class LayoutDesignerScreen : Screen
{
    private const int   BannerHeight         = 1;
    private const int   FooterHeight         = 1;
    private const int   MessageBoxWidth      = 48;
    private const int   PickerMinWidth       = 24;
    private const int   PickerPreferredWidth = 40;
    private const float RatioStep            = 0.05f;

    internal static readonly PaneControlType[] AssignableControlTypes = Enum.GetValues<PaneControlType>()
        .Except(SummaryControl2.UnavailableControlTypes)
        .ToArray();

    // Each label is the column's Statistics_<member> entry in Strings.resx.
    private static readonly Statistics[] ToggleableColumns =
    [
        Statistics.User,
        Statistics.Pri,
        Statistics.Cpu,
        Statistics.AvgCpu,
        Statistics.MaxCpu,
        Statistics.Thrd,
        Statistics.Gpu,
        Statistics.AvgGpu,
        Statistics.MaxGpu,
        Statistics.Mem,
        Statistics.AvgMem,
        Statistics.MaxMem,
        Statistics.Disk,
        Statistics.AvgDisk,
        Statistics.MaxDisk,
        Statistics.Power,
        Statistics.Path,
    ];

    private readonly RunContext             runContext;
    private readonly SummaryControl2        summary;
    private readonly PickerBoxControl       controlTypePicker;
    private readonly PickerBoxControl       columnPicker;
    private readonly TextInputDialogControl saveDialog;
    private readonly MessageBoxControl      messageBox;
    
    private readonly Dictionary<Control, Color> highlightedBorders = new();

    private LayoutTree<SummaryPane> tree = new(SummaryPane.Empty);

    private int     selectedNodeId;
    private string? layoutName;
    private bool    isLoaded;

    public LayoutDesignerScreen(RunContext runContext) : base(runContext.Terminal)
    {
        this.runContext = runContext;

        controlTypePicker = new PickerBoxControl(runContext.Terminal) { Visible = false };
        columnPicker      = new PickerBoxControl(runContext.Terminal) { Visible = false, MultiSelect = true };

        saveDialog = new TextInputDialogControl(runContext.Terminal) {
            Visible = false,
            Title = Strings.LayoutDesignerSaveTitle,
            CharacterFilter = ch => char.IsLetterOrDigit(ch) || ch is '-' or ' '
        };

        messageBox = new MessageBoxControl(runContext.Terminal) {
            Visible = false,
            Buttons = MessageBoxButtons.Ok,
            Height  = MessageBoxControl.PreferredHeight
        };

        CommonControl.Text(controlTypePicker);
        CommonControl.Text(columnPicker);
        CommonControl.Text(saveDialog);
        CommonControl.Text(messageBox);

        summary = new SummaryControl2(runContext.ServiceController, runContext.Terminal, runContext.AppConfig, tree);
        Controls.Add(summary);

        selectedNodeId = tree.Panes().First().Id;
    }

    private void AdjustSelectedRatio(float delta)
    {
        if (summary.FindResizableSplit(selectedNodeId) is not { } split) {
            return;
        }

        if (!tree.AdjustRatio(split.SplitId, split.InFirst ? delta : -delta)) {
            return;
        }

        summary.Resize();
        Draw();
    }

    internal void AssignSelectedPaneControlTypeForTests(PaneControlType newType) =>
        ReassignSelectedPaneControlType(newType);


    private void DrawBanner()
    {
        Terminal.SetCursorPosition(X, Y);
        Terminal.BackgroundColor = runContext.AppConfig.Theme.MenubarBackground;
        Terminal.ForegroundColor = runContext.AppConfig.Theme.MenubarForeground;

        string title = string.Format(CultureInfo.CurrentCulture, Strings.LayoutDesignerTitle, layoutName ?? Strings.LayoutDesignerUntitled);
        int offsetX = Math.Max(0, Width / 2 - title.Length / 2);

        Terminal.WriteEmptyLineTo(offsetX);
        Terminal.Write(title);
        Terminal.WriteEmptyLineTo(Math.Max(0, Width - offsetX - title.Length));
    }

    private void DrawFooter()
    {
        string help = Strings.LayoutDesignerHelp;

        Terminal.SetCursorPosition(X, Y + Height - FooterHeight);
        Terminal.BackgroundColor = runContext.AppConfig.Theme.Background;
        Terminal.ForegroundColor = runContext.AppConfig.Theme.Foreground;

        string shown = help.Length <= Width ? help : help[..Width];
        Terminal.Write(shown);
        Terminal.WriteEmptyLineTo(Math.Max(0, Width - shown.Length));
    }

    internal static int? FindNearest(IReadOnlyDictionary<int, Rectangle> paneBounds, int fromId, ConsoleKey arrow)
    {
        if (!paneBounds.TryGetValue(fromId, out Rectangle from)) {
            return null;
        }

        int? bestId      = null;
        long bestGap     = long.MaxValue;
        long bestOverlap = 0;

        foreach ((int id, Rectangle to) in paneBounds) {
            long overlapX = Math.Min(from.Right, to.Right) - Math.Max(from.Left, to.Left);
            long overlapY = Math.Min(from.Bottom, to.Bottom) - Math.Max(from.Top, to.Top);

            (long gap, long overlap) = arrow switch {
                ConsoleKey.LeftArrow  => ((long)from.Left - to.Right, overlapY),
                ConsoleKey.RightArrow => ((long)to.Left - from.Right, overlapY),
                ConsoleKey.UpArrow    => ((long)from.Top - to.Bottom, overlapX),
                                    _ => ((long)to.Top - from.Bottom, overlapX)
            };

            if (id == fromId || gap < 0 || overlap <= 0) {
                continue;
            }

            if (gap < bestGap || (gap == bestGap && overlap > bestOverlap)) {
                bestId      = id;
                bestGap     = gap;
                bestOverlap = overlap;
            }
        }

        return bestId;
    }

    private int FirstPaneUnder(int nodeId)
    {
        LayoutNode<SummaryPane> node = tree.Nodes[nodeId];
        
        return node.IsSplit 
            ? FirstPaneUnder(node.FirstId) 
            : nodeId;
    }

    private void HighlightSubtree(Control control)
    {
        highlightedBorders[control] = control.BorderColour;
        control.BorderColour = Control.FocusSelectionColour;

        foreach (Control child in control.Controls) {
            HighlightSubtree(child);
        }
    }

    internal string? LayoutName => layoutName;

    private void MoveSelection(ConsoleKey arrow)
    {
        if (FindNearest(summary.PaneBounds, selectedNodeId, arrow) is not { } id) {
            return;
        }

        selectedNodeId = id;
        RefreshSelectionHighlight();
        Draw();
    }

    private void OnColumnPickerKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        columnPicker.KeyPressed(keyInfo, ref handled);

        if (columnPicker.Result == PickerBoxResult.None) {
            return;
        }

        PickerBoxResult result   = columnPicker.Result;
        List<int> checkedIndices = columnPicker.CheckedIndices.ToList();

        columnPicker.Visible  = false;
        Control.RedrawEnabled = true;

        if (result == PickerBoxResult.Ok) {
            Statistics columns = Statistics.Process | Statistics.Pid;

            foreach (int index in checkedIndices) {
                columns |= ToggleableColumns[index];
            }

            SelectedPane = SelectedPane with { ProcessColumns = columns };

            if (summary.PaneControls[selectedNodeId] is ProcessControl processControl) {
                processControl.VisibleColumnsOverride = columns;
                processControl.Resize();
            }
        }

        Draw();
    }

    private void OnControlTypePickerKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        controlTypePicker.KeyPressed(keyInfo, ref handled);

        if (controlTypePicker.Result == PickerBoxResult.None) {
            return;
        }

        PickerBoxResult result = controlTypePicker.Result;
        int selectedIndex = controlTypePicker.SelectedIndex;

        controlTypePicker.Visible = false;
        Control.RedrawEnabled     = true;

        if (result == PickerBoxResult.Ok && selectedIndex >= 0 && selectedIndex < AssignableControlTypes.Length) {
            ReassignSelectedPaneControlType(AssignableControlTypes[selectedIndex]);
        }

        Draw();
    }

    protected override void OnDraw()
    {
        DrawBanner();
        summary.Draw();
        DrawFooter();

        if (controlTypePicker.Visible) {
            controlTypePicker.Draw();
        }
        else if (columnPicker.Visible) {
            columnPicker.Draw();
        }
        else if (saveDialog.Visible) {
            saveDialog.Draw();
        }
        else if (messageBox.Visible) {
            messageBox.Draw();
        }
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        if (controlTypePicker.Visible) {
            OnControlTypePickerKeyPressed(keyInfo, ref handled);
            return;
        }

        if (columnPicker.Visible) {
            OnColumnPickerKeyPressed(keyInfo, ref handled);
            return;
        }

        if (saveDialog.Visible) {
            OnSaveDialogKeyPressed(keyInfo, ref handled);
            return;
        }

        if (messageBox.Visible) {
            OnMessageBoxKeyPressed(keyInfo, ref handled);
            return;
        }

        base.OnKeyPressed(keyInfo, ref handled);

        if (handled) {
            return;
        }

        // On macOS the unshifted +/= key is '=' with no ConsoleKey.
        if (keyInfo.KeyChar == '=') {
            AdjustSelectedRatio(RatioStep);
            handled = true;
            return;
        }

        switch (keyInfo.Key) {
            case ConsoleKey.LeftArrow:
            case ConsoleKey.RightArrow:
            case ConsoleKey.DownArrow:
            case ConsoleKey.UpArrow:
                MoveSelection(keyInfo.Key);
                handled = true;
                break;

            case ConsoleKey.V:
                SplitSelectedPane(Orientation.Row);
                handled = true;
                break;

            case ConsoleKey.H:
                SplitSelectedPane(Orientation.Column);
                handled = true;
                break;

            case ConsoleKey.Enter:
                OpenControlTypePicker();
                handled = true;
                break;

            case ConsoleKey.C:
                OpenColumnPicker();
                handled = true;
                break;

            case ConsoleKey.Add:
            case ConsoleKey.OemPlus:
                AdjustSelectedRatio(RatioStep);
                handled = true;
                break;

            case ConsoleKey.Subtract:
            case ConsoleKey.OemMinus:
                AdjustSelectedRatio(-RatioStep);
                handled = true;
                break;

            case ConsoleKey.Backspace:
            case ConsoleKey.Delete:
                RemoveSelectedPane();
                handled = true;
                break;

            case ConsoleKey.S:
                SaveLayout();
                handled = true;
                break;
        }
    }

    protected override void OnLoad()
    {
        Terminal.CursorVisible = false;

        CommonControl.ThemeDefaults(this, runContext.AppConfig.Theme);
        CommonControl.Theme(controlTypePicker, runContext.AppConfig.Theme);
        CommonControl.Theme(columnPicker, runContext.AppConfig.Theme);
        CommonControl.Theme(saveDialog, runContext.AppConfig.Theme);
        CommonControl.Theme(messageBox, runContext.AppConfig.Theme);

        controlTypePicker.Load();
        columnPicker.Load();
        saveDialog.Load();
        messageBox.Load();

        isLoaded = true;

        // Loads and themes all panes.
        base.OnLoad();
        RefreshSelectionHighlight();

        runContext.ServiceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
    }

    private void OnMessageBoxKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        messageBox.KeyPressed(keyInfo, ref handled);

        if (messageBox.Result == MessageBoxResult.None) {
            return;
        }

        messageBox.Visible    = false;
        Control.RedrawEnabled = true;

        Clear();
        Draw();
    }

    protected override void OnResize()
    {
        summary.X      = X;
        summary.Y      = Y + BannerHeight;
        summary.Width  = Width;
        summary.Height = Math.Max(0, Height - BannerHeight - FooterHeight);

        base.OnResize();
    }

    private void OnSaveDialogKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        saveDialog.KeyPressed(keyInfo, ref handled);

        if (saveDialog.Result == TextInputDialogResult.None) {
            return;
        }

        TextInputDialogResult result = saveDialog.Result;
        string name = saveDialog.Text.Trim();

        saveDialog.Visible    = false;
        Control.RedrawEnabled = true;

        Draw();

        if (result == TextInputDialogResult.Ok && name.Length > 0) {
            SaveLayoutAs(name);
        }
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        lock (DrawingLock) {
            RefreshSelectionHighlight();
        }
    }
    
    protected override void OnUnload()
    {
        runContext.ServiceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;

        highlightedBorders.Clear();

        controlTypePicker.Unload();
        columnPicker.Unload();
        saveDialog.Unload();
        messageBox.Unload();

        isLoaded = false;
        Terminal.CursorVisible = true;

        base.OnUnload();
    }

    public void Open(LayoutTree<SummaryPane> newTree, string? existingName)
    {
        tree           = newTree;
        layoutName     = existingName;
        selectedNodeId = tree.Panes().First().Id;

        summary.Show(tree);
        RefreshSelectionHighlight();
    }

    private void OpenColumnPicker()
    {
        if (SelectedPane.ControlType != PaneControlType.Process) {
            return;
        }

        Control.RedrawEnabled = false;

        Statistics current = SelectedPane.ProcessColumns ?? runContext.AppConfig.VisibleColumns;
        List<string> labels = ToggleableColumns.Select(c => c.ToDisplayText()).ToList();
        List<bool> initiallyChecked = ToggleableColumns.Select(c => (current & c) != 0).ToList();

        columnPicker.Width  = PickerWidth(columnPicker);
        columnPicker.Height = Math.Clamp(PickerBoxControl.GetPreferredHeight(labels.Count), 8, Math.Max(8, Height - 2));
        columnPicker.X      = X + Math.Max(0, (Width - columnPicker.Width) / 2);
        columnPicker.Y      = Y + Math.Max(0, (Height - columnPicker.Height) / 2);
        columnPicker.Title  = Strings.LayoutDesignerColumnsTitle;
        
        columnPicker.SetItems(labels, initiallyChecked);
        columnPicker.Visible = true;
        columnPicker.ShowPickerBox();
    }

    private void OpenControlTypePicker()
    {
        Control.RedrawEnabled = false;

        List<string> labels = AssignableControlTypes.Select(ToDisplayLabel).ToList();
        int currentIndex    = Array.IndexOf(AssignableControlTypes, SelectedPane.ControlType);

        controlTypePicker.Width  = PickerWidth(controlTypePicker);
        controlTypePicker.Height = Math.Clamp( PickerBoxControl.GetPreferredHeight(labels.Count), 8, Math.Max(8, Height - 2));
        controlTypePicker.X      = X + Math.Max(0, (Width - controlTypePicker.Width) / 2);
        controlTypePicker.Y      = Y + Math.Max(0, (Height - controlTypePicker.Height) / 2);
        controlTypePicker.Title  = Strings.LayoutDesignerAssignTitle;
        
        controlTypePicker.SetItems(labels, initialSelectedIndex: Math.Max(0, currentIndex));
        controlTypePicker.Visible = true;
        controlTypePicker.ShowPickerBox();
    }

    private int PickerWidth(PickerBoxControl picker) =>
        Math.Max(PickerMinWidth, Math.Min(Math.Max(PickerPreferredWidth, picker.HelpText.Length + 2), Width - 4));

    private void ReassignSelectedPaneControlType(PaneControlType newType)
    {
        // Process columns only mean something to a Process pane.
        SelectedPane = newType == PaneControlType.Process
            ? SelectedPane with { ControlType = newType }
            : new SummaryPane(newType);

        summary.Show(tree);
        RefreshSelectionHighlight();
        summary.Resize();
    }

    private void RefreshSelectionHighlight()
    {
        foreach ((Control control, Color original) in highlightedBorders) {
            control.BorderColour = original;
        }

        highlightedBorders.Clear();

        if (!isLoaded || !summary.PaneControls.TryGetValue(selectedNodeId, out Control? selected)) {
            return;
        }

        HighlightSubtree(selected);
    }

    private void RemoveSelectedPane()
    {
        int? parentId = tree.FindParentSplitId(selectedNodeId);

        // The selected pane is the root - nothing to merge into.
        if (parentId is not { } pid) {
            return;
        }

        if (!tree.Remove(selectedNodeId)) {
            return;
        }

        summary.Show(tree);

        selectedNodeId = FirstPaneUnder(pid);
        RefreshSelectionHighlight();
        summary.Resize();
        Draw();
    }

    private void SaveLayout()
    {
        Control.RedrawEnabled = false;

        saveDialog.Width  = Math.Clamp(Width - 4, 30, 50);
        saveDialog.Height = TextInputDialogControl.PreferredHeight;
        saveDialog.X      = X + Math.Max(0, (Width - saveDialog.Width) / 2);
        saveDialog.Y      = Y + Math.Max(0, (Height - saveDialog.Height) / 2);

        saveDialog.SetText(layoutName != null && runContext.AppConfig.IsBuiltInLayout(layoutName)
            ? string.Format(CultureInfo.CurrentCulture, Strings.LayoutDesignerCopyName, layoutName)
            : layoutName ?? string.Empty);
        
        saveDialog.Visible = true;
        saveDialog.ShowTextInputDialog();
    }

    private void SaveLayoutAs(string name)
    {
        if (runContext.AppConfig.IsBuiltInLayout(name)) {
            ShowMessageBox(
                Strings.LayoutDesignerBuiltInTitle,
                string.Format(CultureInfo.CurrentCulture, Strings.LayoutDesignerBuiltInText, name));

            return;
        }

        layoutName = name;
        bool saved = runContext.AppConfig.SaveLayout(SummaryControlLayout.FromTree(name, tree));

        if (!saved) {
            ShowMessageBox(Strings.CommonSaveFailedTitle, Strings.LayoutDesignerSaveFailedText);
            return;
        }

        DrawBanner();
    }

    internal int SelectedNodeId => selectedNodeId;

    private SummaryPane SelectedPane
    {
        get => tree.Nodes[selectedNodeId].Pane!;
        set => tree.Nodes[selectedNodeId].Pane = value;
    }

    private void ShowMessageBox(string title, string text)
    {
        Control.RedrawEnabled = false;

        messageBox.Width = Math.Min(MessageBoxWidth, Width);
        messageBox.X     = X + Math.Max(0, (Width - messageBox.Width) / 2);
        messageBox.Y     = Y + Math.Max(0, (Height - messageBox.Height) / 2);
        messageBox.Title = title;
        messageBox.Text  = text;

        messageBox.Visible = true;
        messageBox.ShowMessageBox();
    }

    private void SplitSelectedPane(Orientation orientation)
    {
        (_, int secondId) = tree.Split(selectedNodeId, orientation, SummaryPane.Empty);

        summary.Show(tree);

        // The new empty pane, ready to be assigned straight away.
        selectedNodeId = secondId;

        RefreshSelectionHighlight();
        summary.Resize();
        Draw();
    }

    internal static string ToDisplayLabel(PaneControlType type) => type.ToDisplayText();

    internal LayoutTree<SummaryPane> Tree => tree;
}
