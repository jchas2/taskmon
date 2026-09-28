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

// A live, WYSIWYG editor for a SummaryLayoutTree: the exact same SummaryLayoutRenderer and
// SummaryPaneControlFactory that render a saved tree on the real SUMMARY screen (SummaryControl2)
// render it here too, so what you build is what you get. Reached from Setup Screen's LAYOUTS tab
// (see SetupScreen's 'N' key) via Open(...) followed by ScreenApplication.ShowScreen<
// LayoutDesignerScreen>() - Open() must be called first every time, since this screen is a
// singleton reused across visits and Open() is what actually loads/replaces the tree being edited.
//
// selectedNodeId is a designer-owned "cursor" over the tree's panes, deliberately independent of
// the framework's own Control focus chain (every pane control is built with TabStop = false) -
// arrows always move this cursor and Enter always opens the type picker, rather than being routed
// into whichever composite happens to be selected (which would fight the designer for keys, e.g.
// a Process pane's own arrow-driven list scrolling). Movement is spatial (SpatialNavigation), not
// the tree-order cycling SummaryControl2 uses at runtime - deliberately different, because this is
// a 2D layout canvas where "the pane above/left/right/below on screen" is what an arrow key means,
// unlike a linear tab order between content panes.
public sealed class LayoutDesignerScreen : Screen
{
    private const int BannerHeight = 1;
    private const int FooterHeight = 1;
    private const float RatioStep = 0.05f;

    private static readonly PaneControlType[] assignableControlTypes = Enum.GetValues<PaneControlType>();

    // Mirrors SetupScreen's toggleableColumns table - Process and Pid are always shown (see
    // ProcessControl.IsColumnVisible) so they are never offered as choices here either.
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

    // Every control in the selected pane's subtree whose BorderColour is currently swapped to the
    // focus colour, mapped to the colour it had before - see RefreshSelectionHighlight.
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

        // Layout names become the [section] name in the saved .layout file, and ConfigParser only
        // accepts letters, digits, "-" and spaces there - anything else would save, then fail to
        // parse on the next load. Only those characters can be typed.
        saveDialog = new TextInputDialogControl(runContext.Terminal) {
            Visible = false,
            Title = "Save Layout As",
            CharacterFilter = ch => char.IsLetterOrDigit(ch) || ch is '-' or ' '
        };

        RebuildPaneControls();
    }

    // Loads a tree for editing (existingName null for a brand new, unsaved layout) - always call
    // this before ScreenApplication.ShowScreen<LayoutDesignerScreen>(), since this screen is a
    // registered singleton and this is the only way its content ever changes between visits.
    public void Open(SummaryLayoutTree newTree, string? existingName)
    {
        tree = newTree;
        layoutName = existingName;
        RebuildPaneControls();
    }

    // Exposed for tests - the tree currently being edited (post Split/Remove/reassign mutations).
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

        // Selection here is this screen's own cursor (selectedNodeId), never real keyboard focus -
        // no pane control should compete with that via the framework's own tab-order focusing.
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

    // Selection is shown by the selected pane drawing its own borders in the focus colour -
    // the same BorderColour swap the framework's real focus uses (Control.GotFocus) - rather than
    // an outline painted over the pane from outside. An external outline flickered: panes with a
    // live snapshot subscription (Process, Drivers, charts fed on the tick) repaint their own
    // default-coloured border on every tick, and the outline could only be repainted after that,
    // so every tick showed the plain border for an instant before the highlight came back.
    //
    // Walks the whole subtree because composites don't draw a border of their own - ProcessControl
    // delegates to whichever inner ListView is active, Drivers/Services have a list plus a detail
    // pane - and every bordered piece of the selected pane should read as selected.
    private void RefreshSelectionHighlight()
    {
        foreach ((Control control, Color original) in highlightedBorders) {
            control.BorderColour = original;
        }

        highlightedBorders.Clear();

        // Before Load() the panes haven't had their theme colours applied yet - saving
        // "originals" now would capture pre-theme defaults and later restore those over the
        // themed values. OnLoad applies the highlight once loading is done.
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

        // Routes to Screen's own messageBox, which is what the "Save Failed" error uses.
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

            // Plain S, not Ctrl+S: consoles take Ctrl+S as "pause output" (XOFF on Unix, the same
            // behaviour in the Windows console's default input mode) and hold all output until the
            // next key - it never reaches the app. Typing an 's' into the save-name prompt can't
            // land here, since that InputBox marks every key it receives as handled.
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

        // The new blank pane, ready to be assigned straight away.
        selectedNodeId = secondId;

        RefreshSelectionHighlight();
        Relayout();
        Draw();
    }

    private void RemoveSelectedPane()
    {
        int? parentId = tree.FindParentSplitId(selectedNodeId);

        if (parentId is not { } pid) {
            // The selected pane is the root - nothing to merge into, refuse.
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
            // The sibling was itself a leaf, so pid is now a leaf carrying its content - reuse
            // its already-built control under pid's id rather than throwing it away and
            // building an identical one fresh.
            paneControls.Remove(siblingId);

            if (siblingControl != null) {
                paneControls[pid] = siblingControl;
            }
            else {
                AddPaneControl(tree.Nodes[pid]);
            }
        }

        // If the sibling was itself a split, pid now carries that whole subtree verbatim - the
        // leaf ids underneath are unchanged, so their existing controls are still correct as-is.

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

    // Test-only seam: applies the choice the control picker would have applied, without driving a
    // key press per row to reach the wanted type.
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
            // 'C' only means anything for a Process pane.
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

    // Always prompts, pre-filled with the current name once there is one - Enter overwrites it,
    // editing the name saves a copy alongside it. Re-saving silently over the existing name gave
    // no feedback at all (the banner already showed that name), so it looked like S did nothing.
    // A built-in layout can't be saved over, so editing one pre-fills the name of a copy instead.
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

        // A full Draw() rather than Clear() + Draw(): every pane repaints over its own region,
        // which covers wherever the dialog was.
        Draw();

        if (result == TextInputDialogResult.Ok && name.Length > 0) {
            SaveLayoutAs(name);
        }
    }

    private void SaveLayoutAs(string name)
    {
        if (runContext.AppConfig.IsBuiltInLayout(name)) {
            // The name on its own line: the longest built-in name fills most of the box's width.
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

        // The banner carries the layout name ("Untitled" until the first save) - the visible
        // confirmation that the save took.
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

        // Screen.OnLoad() finishes with Control.OnLoad()'s default behaviour - foreach (Control
        // control in Controls) control.Load() - which is what loads the panes. Never Load() them
        // here as well: a second Load() double-subscribes any pane with its own snapshot handler
        // (ProcessControl, Drivers, ...), and since Unload() only removes one of the two, a
        // replaced pane kept redrawing itself underneath its replacement. The selection highlight
        // is applied after loading, since each pane's own OnLoad re-assigns its themed borders.
        base.OnLoad();
        RefreshSelectionHighlight();

        runContext.ServiceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        try {
            Control.DrawingLockAcquire();

            // Re-asserted every tick (property assignments only, nothing is drawn here) for panes
            // that create bordered children lazily as data arrives - ThermalsControl builds a
            // chart per sensor, themed with the default border colour, the first time it sees it.
            RefreshSelectionHighlight();

            // Charts are passive - they only move when fed, unlike the Process/Drivers/Services
            // panes which subscribe to snapshots themselves. Chart.Add() repaints the chart, and
            // is a no-op visually while a picker is up (Control.Draw checks RedrawEnabled), so
            // history still accumulates underneath the modal.
            SummaryChartFeeder.Feed(tree, paneControls, e.Snapshot, runContext.AppConfig);
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

        // Each pane's next Load() re-themes its borders, so there is nothing to restore here -
        // and a saved "original" from this visit could otherwise be restored over a changed theme.
        highlightedBorders.Clear();

        // The panes themselves are unloaded by base.OnUnload() (Control.OnUnload's default
        // foreach over Controls) - the pickers aren't in Controls, so they're unloaded here.
        controlTypePicker.Unload();
        columnPicker.Unload();
        saveDialog.Unload();

        isLoaded = false;
        Terminal.CursorVisible = true;

        base.OnUnload();
    }
}
