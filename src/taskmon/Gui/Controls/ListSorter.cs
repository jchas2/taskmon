using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls.ListView;

namespace Task.Monitor.Gui.Controls;

internal sealed class ListSorter<T>
{
    private const int ControlGutter    = 1;
    private const int SortControlWidth = 20;

    private readonly IReadOnlyList<ListSortColumn<T>> columns;
    private readonly ListView                         targetView;

    private bool   ascending = true;
    private bool   columnPicked;
    private int    sortIndex;
    private Theme? theme;

    public ListSorter(
        ISystemTerminal                  terminal,
        ListView                         targetView,
        IReadOnlyList<ListSortColumn<T>> columns,
        int                              defaultColumn = 0)
    {
        this.targetView = targetView;
        this.columns    = columns;
        sortIndex       = defaultColumn;

        SortView = new ListView(terminal) {
            Visible  = false,
            TabStop  = true,
            TabIndex = 1
        };

        SortView.ColumnHeaders.Add(new ListViewColumnHeader("SORT BY"));
        SortView.ItemSelected += OnSortViewItemSelected;
    }

    public void ApplyTheme(Theme theme)
    {
        this.theme = theme;
        CommonControl.Theme(SortView, theme);
    }

    private void Close()
    {
        SortView.Visible = false;
        targetView.SetFocus();
    }

    public void DecorateHeaders()
    {
        for (int i = 0; i < columns.Count; i++) {
            ListViewColumnHeader header = targetView.ColumnHeaders[columns[i].ColumnIndex];

            if (i == sortIndex) {
                header.Text             = columns[i].Title + (ascending ? "↑" : "↓");
                header.BackgroundColour = theme?.BackgroundHighlight;
                header.ForegroundColour = theme?.ForegroundHighlight;
            }
            else {
                header.Text             = columns[i].Title;
                header.BackgroundColour = null;
                header.ForegroundColour = null;
            }
        }
    }

    public void Draw()
    {
        if (SortView.Visible) {
            SortView.Draw();
        }
    }

    public ListView FocusTarget => IsSelectionActive ? SortView : targetView;

    public bool IsSelectionActive => SortView.Visible;

    // Returns true when the sort or the layout changed and the owner must resize and redraw.
    public bool KeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        switch (keyInfo.Key) {
            case ConsoleKey.S:
                if (IsSelectionActive) {
                    Close();
                }
                else {
                    Open();
                }

                handled = true;
                return true;

            case ConsoleKey.A:
            case ConsoleKey.D:
                ascending = keyInfo.Key == ConsoleKey.A;
                handled   = true;
                return true;
        }

        if (!IsSelectionActive) {
            return false;
        }

        switch (keyInfo.Key) {
            case ConsoleKey.Escape:
                Close();
                handled = true;
                return true;

            case ConsoleKey.LeftArrow:
            case ConsoleKey.RightArrow:
                handled = true;
                return false;

            case ConsoleKey.UpArrow:
            case ConsoleKey.DownArrow:
            case ConsoleKey.PageUp:
            case ConsoleKey.PageDown:
            case ConsoleKey.Enter:
                columnPicked = false;
                SortView.KeyPressed(keyInfo, ref handled);
                return columnPicked;
        }

        return false;
    }

    public void Layout(int x, int y, int height)
    {
        SortView.X      = x;
        SortView.Y      = y;
        SortView.Width  = SortControlWidth;
        SortView.Height = height;

        SortView.ColumnHeaders[0].Width = SortControlWidth - 2;
    }

    private void OnSortViewItemSelected(object? sender, ListViewItemEventArgs e)
    {
        int index = SortView.SelectedIndex;

        if (index < 0 || index >= columns.Count) {
            return;
        }

        sortIndex    = index;
        columnPicked = true;
        Close();
    }

    private void Open()
    {
        SortView.Items.Clear();

        foreach (ListSortColumn<T> column in columns) {
            SortView.Items.Add(new ListViewItem(column.Title));
        }

        SortView.SelectedIndex = sortIndex;
        SortView.Visible       = true;
        SortView.SetFocus();
    }

    public int PickerWidth => IsSelectionActive ? SortControlWidth + ControlGutter : 0;

    public string Signature => $"{sortIndex}:{(ascending ? "asc" : "desc")}";

    public List<T> Sort(IEnumerable<T> items)
    {
        ListSortColumn<T> column   = columns[sortIndex];
        Comparer<T>       comparer = Comparer<T>.Create(column.Compare);

        IOrderedEnumerable<T> blanksLast = items.OrderBy(column.IsBlank);

        return (ascending
            ? blanksLast.ThenBy(item => item, comparer)
            : blanksLast.ThenByDescending(item => item, comparer)).ToList();
    }

    public ListView SortView { get; }
}
