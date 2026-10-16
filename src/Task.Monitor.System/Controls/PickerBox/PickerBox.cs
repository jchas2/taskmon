using System.Drawing;
using Task.Monitor.Cli.Utils;
using ListViewControl = Task.Monitor.System.Controls.ListView.ListView;
using ListViewColumnHeader = Task.Monitor.System.Controls.ListView.ListViewColumnHeader;
using ListViewItem = Task.Monitor.System.Controls.ListView.ListViewItem;

namespace Task.Monitor.System.Controls.PickerBox;

public sealed class PickerBox : Control
{
    private const int MinWidth     = 20;
    private const int MinHeight    = 6;
    private const int ChromeHeight = 4;

    private readonly ListViewControl list;

    public static int GetPreferredHeight(int rowCount) => Math.Max(MinHeight, rowCount + ChromeHeight);

    public PickerBox(ISystemTerminal terminal) : base(terminal)
    {
        list = new ListViewControl(terminal) {
            EnableScroll      = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = false,
            ShowBorder        = false,
            Visible           = true
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

    public bool MultiSelect { get; set; }

    public PickerBoxResult Result { get; private set; } = PickerBoxResult.None;

    public int SelectedIndex => list.SelectedIndex;

    public IEnumerable<int> CheckedIndices =>
        list.Items
            .Select((item, index) => (item, index))
            .Where(pair => pair.item.Checked)
            .Select(pair => pair.index);

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
        list.X      = X + 1;
        list.Y      = Y + 2;
        list.Width  = Math.Max(0, Width - 2);
        list.Height = Math.Max(1, Height - ChromeHeight);

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
        string title    = Title.Length > 0 ? $" {Title} " : string.Empty;
        int titleLen    = Math.Min(title.Length, dialogWidth);
        int leftDashes  = (dialogWidth - titleLen) / 2;
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
