using System.Drawing;
using Task.Monitor.Cli.Utils;
using ListViewControl = Task.Monitor.System.Controls.ListView.ListView;
using ListViewColumnHeader = Task.Monitor.System.Controls.ListView.ListViewColumnHeader;
using ListViewItem = Task.Monitor.System.Controls.ListView.ListViewItem;

namespace Task.Monitor.System.Controls.PickerBox;

// A small bordered modal wrapping a scrollable ListView, following the exact same composition
// and show/hide contract as DriveInputBox: not added to a parent's Controls, not part of the
// normal focus chain - a caller shows it via Visible = true plus Control.RedrawEnabled = false,
// then explicitly forwards Draw()/KeyPressed() to it from its own OnDraw/OnKeyPressed while
// Visible is true (see DiskSpaceControl's driveInputBox for the reference pattern).
//
// Deliberately knows nothing about what it is picking - a caller supplies plain row labels via
// SetItems and reads back SelectedIndex (MultiSelect false) or CheckedIndices (MultiSelect true)
// once Result is Ok, and maps those indices back to its own domain (e.g. a PaneControlType or a
// Statistics column flag). That keeps this control reusable from any picker use case rather than
// tied to Summary2's types, which is also why it lives in Task.Monitor.System rather than taskmon.
public sealed class PickerBox : Control
{
    private const int MinWidth = 20;
    private const int MinHeight = 6;

    // Rows consumed by everything other than the list itself: top border, the spacer row under
    // the title, the help row, and the bottom border.
    private const int ChromeHeight = 4;

    private readonly ListViewControl list;

    // Lets a caller size the box to fit its row count without needing to know how many extra
    // rows the chrome itself consumes.
    public static int GetPreferredHeight(int rowCount) => Math.Max(MinHeight, rowCount + ChromeHeight);

    public PickerBox(ISystemTerminal terminal) : base(terminal)
    {
        list = new ListViewControl(terminal) {
            EnableScroll = true,
            EnableRowSelect = true,
            ShowColumnHeaders = false,
            ShowBorder = false,
            Visible = true
        };

        list.ColumnHeaders.Add(new ListViewColumnHeader(string.Empty));
    }

    public Color DialogBackgroundColour { get; set; } = ConsolePalette.Gray;
    public Color DialogBorderColour { get; set; } = ConsolePalette.Black;
    public Color DialogForegroundColour { get; set; } = ConsolePalette.Black;

    public Color ListBackgroundHighlightColour
    {
        get => list.BackgroundHighlightColour;
        set => list.BackgroundHighlightColour = value;
    }

    public Color ListBackgroundHighlightInactiveColour
    {
        get => list.BackgroundHighlightInactiveColour;
        set => list.BackgroundHighlightInactiveColour = value;
    }

    public Color ListForegroundHighlightColour
    {
        get => list.ForegroundHighlightColour;
        set => list.ForegroundHighlightColour = value;
    }

    public Color ListForegroundHighlightInactiveColour
    {
        get => list.ForegroundHighlightInactiveColour;
        set => list.ForegroundHighlightInactiveColour = value;
    }

    public string Title { get; set; } = string.Empty;

    // Single-select (a control-type picker): Enter confirms whatever row is highlighted. Multi
    // -select (a column picker): the list grows checkboxes, Space (handled by ListView itself)
    // toggles one, and Enter confirms whatever ends up checked regardless of which row is
    // highlighted at the time.
    public bool MultiSelect { get; set; }

    public PickerBoxResult Result { get; private set; } = PickerBoxResult.None;

    // Valid once Result == Ok and MultiSelect is false.
    public int SelectedIndex => list.SelectedIndex;

    // Valid once Result == Ok and MultiSelect is true. ListView has no built-in "get checked
    // items" helper - this is the same hand-rolled Items.Where(item => item.Checked) pattern
    // ProcessControl.CheckedProcesses already uses.
    public IEnumerable<int> CheckedIndices =>
        list.Items
            .Select((item, index) => (item, index))
            .Where(pair => pair.item.Checked)
            .Select(pair => pair.index);

    // initiallyChecked (MultiSelect only) lets a caller reopen the picker with whatever was
    // chosen last time already ticked, indexed the same as labels. initialSelectedIndex
    // (single-select) likewise lets a caller reopen already highlighting the current value,
    // rather than always restarting at row 0.
    public void SetItems(
        IReadOnlyList<string> labels,
        IReadOnlyList<bool>? initiallyChecked = null,
        int initialSelectedIndex = 0)
    {
        list.ShowCheckboxes = MultiSelect;
        list.Items.Clear();

        for (int i = 0; i < labels.Count; i++) {
            ListViewItem item = new(labels[i]) {
                Checked = MultiSelect && initiallyChecked != null && i < initiallyChecked.Count && initiallyChecked[i]
            };

            list.Items.Add(item);
        }

        if (list.Items.Count > 0) {
            list.SelectedIndex = Math.Clamp(initialSelectedIndex, 0, list.Items.Count - 1);
        }

        Result = PickerBoxResult.None;
    }

    public void ShowPickerBox()
    {
        OnResize();
        OnDraw();
    }

    protected override void OnResize()
    {
        list.X = X + 1;
        list.Y = Y + 2;
        list.Width = Math.Max(0, Width - 2);
        list.Height = Math.Max(1, Height - ChromeHeight);
        // In MultiSelect mode ListView draws a "[x] " checkbox ahead of the first column - a label
        // column as wide as the whole list then no longer fits, and ListView.DrawItem skips any
        // column that doesn't fit, so every row showed only its checkbox and no label.
        int checkboxWidth = MultiSelect ? ListViewControl.CheckboxWidth : 0;
        list.ColumnHeaders[0].Width = Math.Max(0, list.Width - checkboxWidth);
        list.Resize();

        base.OnResize();
    }

    protected override void OnDraw()
    {
        if (Width < MinWidth || Height < MinHeight) {
            return;
        }

        DrawFrame();
        DrawList();
        DrawFooter();
    }

    // list.Draw() is the inherited Control wrapper, which itself checks the static
    // Control.RedrawEnabled before doing anything - a caller showing this box modally (the whole
    // point of a modal) sets that false first, to suppress unrelated background repaints while it
    // is up. That would silently skip the inner list's own draw even though PickerBox's own
    // frame/footer render fine, since those are written directly in OnDraw rather than through
    // another gated Draw() call. Force it true for just this one call, regardless of the ambient
    // value, then restore whatever the caller had it set to.
    private void DrawList()
    {
        bool redrawEnabled = Control.RedrawEnabled;
        Control.RedrawEnabled = true;

        try {
            list.Draw();
        }
        finally {
            Control.RedrawEnabled = redrawEnabled;
        }
    }

    private void DrawFrame()
    {
        DrawRectangle(X, Y, Width, Height, DialogBackgroundColour);

        int dialogWidth = Width - 2;
        string title = Title.Length > 0 ? $" {Title} " : string.Empty;
        int titleLen = Math.Min(title.Length, dialogWidth);
        int leftDashes = (dialogWidth - titleLen) / 2;
        int rightDashes = dialogWidth - titleLen - leftDashes;

        Terminal.BackgroundColor = DialogBackgroundColour;
        Terminal.ForegroundColor = DialogForegroundColour;
        Terminal.SetCursorPosition(X, Y);
        Terminal.Write('╭');
        Terminal.Write('─', leftDashes);
        Terminal.Write(titleLen < title.Length ? title[..titleLen] : title);
        Terminal.Write('─', rightDashes);
        Terminal.Write('╮');

        string spacer = new(' ', dialogWidth);
        Terminal.SetCursorPosition(X, Y + 1);
        Terminal.Write($"│{spacer}│");

        // Side borders for every row the list occupies - the list itself only paints its own
        // content columns, not the dialog's outer frame either side of it.
        for (int row = 0; row < list.Height; row++) {
            Terminal.SetCursorPosition(X, list.Y + row);
            Terminal.Write('│');
            Terminal.SetCursorPosition(X + Width - 1, list.Y + row);
            Terminal.Write('│');
        }
    }

    private void DrawFooter()
    {
        int dialogWidth = Width - 2;
        int y = list.Y + list.Height;

        string help = MultiSelect
            ? "↑ ↓ browse   Space toggle   ↵ confirm   Esc cancel"
            : "↑ ↓ browse   ↵ select   Esc cancel";

        Terminal.BackgroundColor = DialogBackgroundColour;
        Terminal.ForegroundColor = DialogForegroundColour;
        Terminal.SetCursorPosition(X, y);
        Terminal.Write($"│{help.CentreWithLength(dialogWidth)}│");

        Terminal.SetCursorPosition(X, ++y);
        Terminal.Write('╰');
        Terminal.Write('─', dialogWidth);
        Terminal.Write('╯');
    }

    // Redraws only whatever the key actually changed, rather than a full OnDraw() after every
    // press - matches DriveInputBox's OnKeyPressed for the same reason (ListView.OnKeyPressed
    // already redraws itself incrementally; a trailing full OnDraw() here would repaint the
    // entire frame on top of that on every single arrow press).
    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        Result = PickerBoxResult.None;
        handled = true;

        switch (keyInfo.Key) {
            case ConsoleKey.Enter:
                Result = PickerBoxResult.Ok;
                break;

            case ConsoleKey.Escape:
                Result = PickerBoxResult.Cancel;
                break;

            default:
                list.KeyPressed(keyInfo, ref handled);
                break;
        }
    }
}
