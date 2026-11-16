using System.Drawing;
using Task.Monitor.Cli.Utils;
using ListViewColumnHeader = Task.Monitor.System.Controls.ListView.ListViewColumnHeader;
using ListViewControl = Task.Monitor.System.Controls.ListView.ListView;
using ListViewItem = Task.Monitor.System.Controls.ListView.ListViewItem;

namespace Task.Monitor.System.Controls.PickerBox;

public sealed class PickerBox : Dialog
{
    private const int MinWidth     = 20;
    private const int MinHeight    = 6;
    private const int ChromeHeight = 4;

    private readonly ListViewControl list;

    private int[] order = [];

    public PickerBox(ISystemTerminal terminal) : base(terminal)
    {
        list = new ListViewControl(terminal) {
            EnableScroll      = true,
            Focused           = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = false,
            ShowBorder        = false,
            Visible           = true,
            BackgroundColour  = ConsolePalette.Gray,
            ForegroundColour  = ConsolePalette.Black
        };

        list.ColumnHeaders.Add(new ListViewColumnHeader(string.Empty));
    }

    public IEnumerable<int> CheckedIndices =>
        list.Items
            .Select((item, index) => (item, index))
            .Where(pair => pair.item.Checked)
            .Select(pair => order[pair.index])
            .Order();

    private void DrawFooter()
    {
        int y = list.Y + list.Height;

        DrawRow(y, HelpText);
        DrawBottomLine(++y);
    }

    private void DrawFrame()
    {
        DrawBackground();
        DrawTopLine();
        DrawRow(Y + 1, string.Empty);

        for (int row = 0; row < list.Height; row++) {
            DrawSide(X, list.Y + row);
        }

        DrawScrollIndicators();
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

    private void DrawScrollIndicators()
    {
        (int upRow, int downRow) = list.GetScrollIndicatorRows();

        for (int row = 0; row < list.Height; row++) {
            int y = list.Y + row;

            if (y == upRow || y == downRow) {
                Terminal.BackgroundColor = DialogBackgroundColour;
                Terminal.ForegroundColor = DialogForegroundColour;
                Terminal.SetCursorPosition(X + Width - 1, y);
                Terminal.Write(y == upRow ? '▲' : '▼');
                continue;
            }

            DrawSide(X + Width - 1, y);
        }
    }

    public static int GetPreferredHeight(int rowCount) => Math.Max(MinHeight, rowCount + ChromeHeight);

    public string HelpText { get; set; } = string.Empty;

    public Color ListBackgroundColour
    {
        get => list.BackgroundColour;
        set => list.BackgroundColour = value;
    }

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

    public Color ListForegroundColour
    {
        get => list.ForegroundColour;
        set => list.ForegroundColour = value;
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

    public bool MultiSelect { get; set; }

    protected override void OnDraw()
    {
        if (Width < MinWidth || Height < MinHeight) {
            return;
        }

        DrawFrame();
        DrawList();
        DrawFooter();
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
                DrawScrollIndicators();
                break;
        }
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

    public PickerBoxResult Result { get; private set; } = PickerBoxResult.None;

    public int SelectedIndex =>
        list.SelectedIndex >= 0 && list.SelectedIndex < order.Length
            ? order[list.SelectedIndex]
            : list.SelectedIndex;

    public void SetItems(
        IReadOnlyList<string> labels,
        IReadOnlyList<bool>? initiallyChecked = null,
        int? initialSelectedIndex = null)
    {
        list.ShowCheckboxes = MultiSelect;
        list.Items.Clear();

        order = Enumerable.Range(0, labels.Count)
            .OrderBy(index => labels[index], StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (int index in order) {
            ListViewItem item = new(labels[index]) {
                Checked = MultiSelect && initiallyChecked != null && index < initiallyChecked.Count && initiallyChecked[index]
            };

            list.Items.Add(item);
        }

        if (list.Items.Count > 0) {
            list.SelectedIndex = initialSelectedIndex is { } initial
                ? Array.IndexOf(order, Math.Clamp(initial, 0, list.Items.Count - 1))
                : 0;
        }

        Result = PickerBoxResult.None;
    }

    public void ShowPickerBox()
    {
        OnResize();
        OnDraw();
    }
}
