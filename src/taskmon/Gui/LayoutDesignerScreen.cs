using System.Drawing;
using System.Text.RegularExpressions;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.TextInputDialog;
using Task.Monitor.System.Controls.MessageBox;
using Task.Monitor.System.Controls.PickerBox;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Services;
using PickerBoxControl = Task.Monitor.System.Controls.PickerBox.PickerBox;
using TextInputDialogControl = Task.Monitor.System.Controls.TextInputDialog.TextInputDialog;

namespace Task.Monitor.Gui;

// A live WYSIWYG editor the SummaryLayout control.
public sealed class LayoutDesignerScreen : Screen
{
    private const int BannerHeight = 1;
    private const int FooterHeight = 1;
    private const float RatioStep = 0.05f;

    private static readonly PaneControlType[] assignableControlTypes = Enum.GetValues<PaneControlType>();

    // TODO: Dup of SetupScreen's toggleableColumns table. Need a CommonControl functions class.
    private static readonly (Statistics Statistic, string Label)[] toggleableColumns =
    [
        (Statistics.User, "User"),
        (Statistics.Pri, "Priority"),
        (Statistics.Cpu, "CPU %"),
        (Statistics.AvgCpu, "Average CPU %"),
        (Statistics.MaxCpu, "Max CPU %"),
        (Statistics.Thrd, "Threads"),
        (Statistics.Gpu, "GPU %"),
        (Statistics.AvgGpu, "Average GPU %"),
        (Statistics.MaxGpu, "Max GPU %"),
        (Statistics.Mem, "Memory"),
        (Statistics.AvgMem, "Average Memory"),
        (Statistics.MaxMem, "Max Memory"),
        (Statistics.Disk, "Disk"),
        (Statistics.AvgDisk, "Average Disk"),
        (Statistics.MaxDisk, "Max Disk"),
        (Statistics.Power, "Power"),
        (Statistics.Path, "Path"),
    ];

    private readonly RunContext runContext;
    private readonly SummaryLayoutRenderer renderer = new();
    private readonly Dictionary<int, Control> paneControls = new();
    private readonly PickerBoxControl controlTypePicker;
    private readonly PickerBoxControl columnPicker;
    private readonly TextInputDialogControl saveDialog;
    private readonly Dictionary<Control, Color> highlightedBorders = new();
    private SummaryLayoutTree tree = SummaryLayoutTree.CreateEmpty();

    private int selectedNodeId;
    private string? layoutName;
    private bool isLoaded;

    public LayoutDesignerScreen(RunContext runContext) : base(runContext.Terminal)
    {
        this.runContext = runContext;

        controlTypePicker = new PickerBoxControl(runContext.Terminal) { Visible = false };
        columnPicker = new PickerBoxControl(runContext.Terminal) { Visible = false, MultiSelect = true };

        saveDialog = new TextInputDialogControl(runContext.Terminal) {
            Visible = false,
            Title = "Save Layout As",
            CharacterFilter = ch => char.IsLetterOrDigit(ch) || ch is '-' or ' '
        };

        RebuildPaneControls();
    }

    public void Open(SummaryLayoutTree newTree, string? existingName)
    {
        tree = newTree;
        layoutName = existingName;
        RebuildPaneControls();
    }

    internal SummaryLayoutTree Tree => tree;

    internal int SelectedNodeId => selectedNodeId;

    internal string? LayoutName => layoutName;

    private void RebuildPaneControls()
    {
        if (isLoaded) {
            foreach (Control control in paneControls.Values) {
                control.Unload();
            }
        }

        Controls.Clear();
        paneControls.Clear();
        highlightedBorders.Clear();

        foreach (SummaryLayoutNode pane in tree.Panes()) {
            AddPaneControl(pane);
        }

        selectedNodeId = tree.Panes().First().Id;
        RefreshSelectionHighlight();
    }

    private void AddPaneControl(SummaryLayoutNode pane)
    {
        Control control = SummaryPaneControlFactory.Create(pane, runContext.ServiceController, Terminal, runContext.AppConfig);
        control.TabStop = false;

        paneControls[pane.Id] = control;
        Controls.Add(control);
        ApplyTheme(control);

        if (isLoaded) {
            control.Load();
        }
    }

    private void RemovePaneControl(int paneId)
    {
        if (!paneControls.Remove(paneId, out Control? control)) {
            return;
        }

        if (isLoaded) {
            control.Unload();
        }

        Controls.Remove(control);
    }

    private void ApplyTheme(Control control) =>
        SummaryPaneTheme.Apply(control, runContext.AppConfig);

    private int ContentY => Y + BannerHeight;

    private int ContentHeight => Math.Max(0, Height - BannerHeight - FooterHeight);

    private void Relayout() =>
        renderer.Layout(tree, paneControls, X, ContentY, Width, ContentHeight);

    protected override void OnResize()
    {
        Relayout();
        base.OnResize();
    }

    protected override void OnDraw()
    {
        DrawBanner();

        foreach (Control control in paneControls.Values) {
            control.Draw();
        }

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
    }

    private void DrawBanner()
    {
        Terminal.SetCursorPosition(X, Y);
        Terminal.BackgroundColor = runContext.AppConfig.Theme.MenubarBackground;
        Terminal.ForegroundColor = runContext.AppConfig.Theme.MenubarForeground;

        string title = $"LAYOUT DESIGNER - {layoutName ?? "Untitled"}";
        int offsetX = Math.Max(0, Width / 2 - title.Length / 2);

        Terminal.WriteEmptyLineTo(offsetX);
        Terminal.Write(title);
        Terminal.WriteEmptyLineTo(Math.Max(0, Width - offsetX - title.Length));
    }

    private void DrawFooter()
    {
        const string help =
            "←↑→↓ Move  Ctrl+→/↓ Split  ↵ Assign  C Columns  " +
            "+/- Resize  Del Remove  S Save  Esc Back";

        Terminal.SetCursorPosition(X, Y + Height - FooterHeight);
        Terminal.BackgroundColor = runContext.AppConfig.Theme.Background;
        Terminal.ForegroundColor = runContext.AppConfig.Theme.Foreground;

        string shown = help.Length <= Width ? help : help[..Width];
        Terminal.Write(shown);
        Terminal.WriteEmptyLineTo(Math.Max(0, Width - shown.Length));
    }

    private void RefreshSelectionHighlight()
    {
        foreach ((Control control, Color original) in highlightedBorders) {
            control.BorderColour = original;
        }

        highlightedBorders.Clear();

        if (!isLoaded || !paneControls.TryGetValue(selectedNodeId, out Control? selected)) {
            return;
        }

        HighlightSubtree(selected);
    }

    private void HighlightSubtree(Control control)
    {
        highlightedBorders[control] = control.BorderColour;
        control.BorderColour = Control.FocusSelectionColour;

        foreach (Control child in control.Controls) {
            HighlightSubtree(child);
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

        // Routes to Screen's messageBox.
        base.OnKeyPressed(keyInfo, ref handled);

        if (handled) {
            return;
        }

        bool ctrl = keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control);

        switch (keyInfo.Key) {
            case ConsoleKey.LeftArrow when !ctrl:
                MoveSelection(SpatialDirection.Left);
                handled = true;
                break;

            case ConsoleKey.RightArrow when ctrl:
                SplitSelectedPane(Orientation.Row);
                handled = true;
                break;

            case ConsoleKey.RightArrow:
                MoveSelection(SpatialDirection.Right);
                handled = true;
                break;

            case ConsoleKey.DownArrow when ctrl:
                SplitSelectedPane(Orientation.Column);
                handled = true;
                break;

            case ConsoleKey.DownArrow:
                MoveSelection(SpatialDirection.Down);
                handled = true;
                break;

            case ConsoleKey.UpArrow:
                MoveSelection(SpatialDirection.Up);
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

    private void MoveSelection(SpatialDirection direction)
    {
        int? nextId = SpatialNavigation.FindNearest(renderer.PaneBounds, selectedNodeId, direction);

        if (nextId is not { } id) {
            return;
        }

        selectedNodeId = id;
        RefreshSelectionHighlight();
        Draw();
    }

    private void SplitSelectedPane(Orientation orientation)
    {
        (int firstId, int secondId) = tree.Split(selectedNodeId, orientation);

        RemovePaneControl(selectedNodeId);
        AddPaneControl(tree.Nodes[firstId]);
        AddPaneControl(tree.Nodes[secondId]);

        // The new empty pane, ready to be assigned straight away.
        selectedNodeId = secondId;

        RefreshSelectionHighlight();
        Relayout();
        Draw();
    }

    private void RemoveSelectedPane()
    {
        int? parentId = tree.FindParentSplitId(selectedNodeId);

        if (parentId is not { } pid) {
            // The selected pane is the root - nothing to merge into.
            return;
        }

        SummaryLayoutNode parent = tree.Nodes[pid];
        int siblingId = parent.FirstId == selectedNodeId ? parent.SecondId : parent.FirstId;
        bool siblingWasLeaf = !tree.Nodes[siblingId].IsSplit;
        paneControls.TryGetValue(siblingId, out Control? siblingControl);

        if (!tree.Remove(selectedNodeId)) {
            return;
        }

        RemovePaneControl(selectedNodeId);

        if (siblingWasLeaf) {
            paneControls.Remove(siblingId);

            if (siblingControl != null) {
                paneControls[pid] = siblingControl;
            }
            else {
                AddPaneControl(tree.Nodes[pid]);
            }
        }

        selectedNodeId = FirstPaneUnder(pid);
        RefreshSelectionHighlight();
        Relayout();
        Draw();
    }

    private int FirstPaneUnder(int nodeId)
    {
        SummaryLayoutNode node = tree.Nodes[nodeId];
        return node.IsSplit ? FirstPaneUnder(node.FirstId) : nodeId;
    }

    private void AdjustSelectedRatio(float delta)
    {
        int? parentId = tree.FindParentSplitId(selectedNodeId);

        if (parentId is not { } pid) {
            return;
        }

        SummaryLayoutNode parent = tree.Nodes[pid];
        float signedDelta = parent.FirstId == selectedNodeId ? delta : -delta;

        if (!tree.AdjustRatio(pid, signedDelta)) {
            return;
        }

        Relayout();
        Draw();
    }

    private void OpenControlTypePicker()
    {
        Control.RedrawEnabled = false;

        List<string> labels = assignableControlTypes.Select(ToDisplayLabel).ToList();
        int currentIndex = Array.IndexOf(assignableControlTypes, tree.Nodes[selectedNodeId].ControlType);

        controlTypePicker.Width = Math.Clamp(Width - 4, 24, 40);
        controlTypePicker.Height = Math.Clamp(
            PickerBoxControl.GetPreferredHeight(labels.Count), 8, Math.Max(8, Height - 2));
        controlTypePicker.X = X + Math.Max(0, (Width - controlTypePicker.Width) / 2);
        controlTypePicker.Y = Y + Math.Max(0, (Height - controlTypePicker.Height) / 2);
        controlTypePicker.Title = "Assign control";
        controlTypePicker.SetItems(labels, initialSelectedIndex: Math.Max(0, currentIndex));
        controlTypePicker.Visible = true;
        controlTypePicker.ShowPickerBox();
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
        Control.RedrawEnabled = true;

        if (result == PickerBoxResult.Ok && selectedIndex >= 0 && selectedIndex < assignableControlTypes.Length) {
            ReassignSelectedPaneControlType(assignableControlTypes[selectedIndex]);
        }

        Draw();
    }

    internal void AssignSelectedPaneControlTypeForTests(PaneControlType newType) =>
        ReassignSelectedPaneControlType(newType);

    private void ReassignSelectedPaneControlType(PaneControlType newType)
    {
        SummaryLayoutNode pane = tree.Nodes[selectedNodeId];
        pane.ControlType = newType;

        if (newType != PaneControlType.Process) {
            pane.ProcessColumns = null;
        }

        RemovePaneControl(selectedNodeId);
        AddPaneControl(pane);
        RefreshSelectionHighlight();
        Relayout();
    }

    private static string ToDisplayLabel(PaneControlType type) =>
        Regex.Replace(type.ToString(), "(?<!^)([A-Z])", " $1");

    private void OpenColumnPicker()
    {
        if (tree.Nodes[selectedNodeId].ControlType != PaneControlType.Process) {
            return;
        }

        Control.RedrawEnabled = false;

        Statistics current = tree.Nodes[selectedNodeId].ProcessColumns ?? runContext.AppConfig.VisibleColumns;
        List<string> labels = toggleableColumns.Select(c => c.Label).ToList();
        List<bool> initiallyChecked = toggleableColumns.Select(c => (current & c.Statistic) != 0).ToList();

        columnPicker.Width = Math.Clamp(Width - 4, 24, 40);
        columnPicker.Height = Math.Clamp(
            PickerBoxControl.GetPreferredHeight(labels.Count), 8, Math.Max(8, Height - 2));
        columnPicker.X = X + Math.Max(0, (Width - columnPicker.Width) / 2);
        columnPicker.Y = Y + Math.Max(0, (Height - columnPicker.Height) / 2);
        columnPicker.Title = "Columns";
        columnPicker.SetItems(labels, initiallyChecked);
        columnPicker.Visible = true;
        columnPicker.ShowPickerBox();
    }

    private void OnColumnPickerKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        columnPicker.KeyPressed(keyInfo, ref handled);

        if (columnPicker.Result == PickerBoxResult.None) {
            return;
        }

        PickerBoxResult result = columnPicker.Result;
        List<int> checkedIndices = columnPicker.CheckedIndices.ToList();

        columnPicker.Visible = false;
        Control.RedrawEnabled = true;

        if (result == PickerBoxResult.Ok) {
            Statistics columns = Statistics.Process | Statistics.Pid;

            foreach (int index in checkedIndices) {
                columns |= toggleableColumns[index].Statistic;
            }

            tree.Nodes[selectedNodeId].ProcessColumns = columns;

            if (paneControls[selectedNodeId] is ProcessControl processControl) {
                processControl.VisibleColumnsOverride = columns;
                processControl.Resize();
            }
        }

        Draw();
    }

    private void SaveLayout()
    {
        Control.RedrawEnabled = false;

        saveDialog.Width = Math.Clamp(Width - 4, 30, 50);
        saveDialog.Height = TextInputDialogControl.PreferredHeight;
        saveDialog.X = X + Math.Max(0, (Width - saveDialog.Width) / 2);
        saveDialog.Y = Y + Math.Max(0, (Height - saveDialog.Height) / 2);

        saveDialog.SetText(layoutName != null && runContext.AppConfig.IsBuiltInLayout(layoutName)
            ? $"{layoutName} Copy"
            : layoutName ?? string.Empty);
        
        saveDialog.Visible = true;
        saveDialog.ShowTextInputDialog();
    }

    private void OnSaveDialogKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        saveDialog.KeyPressed(keyInfo, ref handled);

        if (saveDialog.Result == TextInputDialogResult.None) {
            return;
        }

        TextInputDialogResult result = saveDialog.Result;
        string name = saveDialog.Text.Trim();

        saveDialog.Visible = false;
        Control.RedrawEnabled = true;

        Draw();

        if (result == TextInputDialogResult.Ok && name.Length > 0) {
            SaveLayoutAs(name);
        }
    }

    private void SaveLayoutAs(string name)
    {
        if (runContext.AppConfig.IsBuiltInLayout(name)) {
            ShowMessageBox(
                "Built-in Layout",
                $"'{name}'\nis a built-in layout.\nSave it under a new name.",
                MessageBoxButtons.Ok,
                () => { });

            return;
        }

        layoutName = name;
        bool saved = runContext.AppConfig.SaveLayout(SummaryControlLayout.FromTree(name, tree));

        if (!saved) {
            ShowMessageBox(
                "Save Failed", "An error occurred saving the layout.", MessageBoxButtons.Ok, () => { });
            return;
        }

        DrawBanner();
    }

    protected override void OnLoad()
    {
        Terminal.CursorVisible = false;

        BackgroundColour = runContext.AppConfig.Theme.Background;
        ForegroundColour = runContext.AppConfig.Theme.Foreground;

        DialogBackgroundColour = runContext.AppConfig.Theme.HeaderBackground;
        DialogBorderColour = runContext.AppConfig.Theme.HeaderForeground;
        DialogForegroundColour = runContext.AppConfig.Theme.HeaderForeground;

        ApplyPickerTheme(controlTypePicker);
        controlTypePicker.Load();

        ApplyPickerTheme(columnPicker);
        columnPicker.Load();

        saveDialog.DialogBackgroundColour = runContext.AppConfig.Theme.HeaderBackground;
        saveDialog.DialogForegroundColour = runContext.AppConfig.Theme.HeaderForeground;
        saveDialog.DialogButtonBackgroundColour = runContext.AppConfig.Theme.BackgroundHighlight;
        saveDialog.DialogButtonForegroundColour = runContext.AppConfig.Theme.ForegroundHighlight;
        saveDialog.FieldBackgroundColour = runContext.AppConfig.Theme.Background;
        saveDialog.FieldForegroundColour = runContext.AppConfig.Theme.Foreground;
        saveDialog.Load();

        foreach (Control control in paneControls.Values) {
            ApplyTheme(control);
        }

        isLoaded = true;

        base.OnLoad();
        RefreshSelectionHighlight();

        runContext.ServiceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        try {
            Control.DrawingLockAcquire();
            RefreshSelectionHighlight();
            
            SummaryChartFeeder.Feed(
                tree, 
                paneControls, 
                e.Snapshot, 
                runContext.AppConfig);
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    private void ApplyPickerTheme(PickerBoxControl picker)
    {
        picker.DialogBackgroundColour = runContext.AppConfig.Theme.HeaderBackground;
        picker.DialogBorderColour = runContext.AppConfig.Theme.HeaderForeground;
        picker.DialogForegroundColour = runContext.AppConfig.Theme.HeaderForeground;
        picker.ListBackgroundHighlightColour = runContext.AppConfig.Theme.BackgroundHighlight;
        picker.ListForegroundHighlightColour = runContext.AppConfig.Theme.ForegroundHighlight;
        picker.ListBackgroundHighlightInactiveColour = runContext.AppConfig.Theme.BackgroundHighlightInactive;
        picker.ListForegroundHighlightInactiveColour = runContext.AppConfig.Theme.ForegroundHighlightInactive;
    }

    protected override void OnUnload()
    {
        runContext.ServiceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;

        highlightedBorders.Clear();

        controlTypePicker.Unload();
        columnPicker.Unload();
        saveDialog.Unload();

        isLoaded = false;
        Terminal.CursorVisible = true;

        base.OnUnload();
    }
}
