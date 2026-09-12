using System.Diagnostics;
using System.Drawing;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Controls.ListView;

public class ListView : Control
{
    // The Collections act as a proxy for updates to the underlying List<T>.
    // This provides a clean api for interacting with Collections on the ListView
    // control, similar to the Win32 ListView common control. 
    private readonly ListViewColumnHeaderCollection columnHeaderCollection;
    private readonly ListViewItemCollection itemCollection;

    // The containers holding the List<T> for rendering. We don't expose them via a public api.
    private List<ListViewColumnHeader> columnHeaders = [];
    private List<ListViewItem> items = [];

    private ViewPort viewPort = new();

    private readonly AnsiScreenBuffer frame = new();

    private const int DefaultColumnWidth = 30;
    private const int DefaultHeaderWidth = 80;

    private const string CheckedText   = "[x] ";
    private const string UnCheckedText = "[ ] ";

    public const int CheckboxWidth = 4;
    
    public event EventHandler<ListViewItemEventArgs>? ItemClicked;
    public event EventHandler<ListViewItemEventArgs>? ItemSelected;

    public ListView(ISystemTerminal terminal)
        : base(terminal)
    {
        itemCollection = new ListViewItemCollection(this);
        columnHeaderCollection = new ListViewColumnHeaderCollection(this);

        EnableRowSelect = true;
        EnableScroll = true;
        ShowBorder = true;
        ShowColumnHeaders = true;
    }
    
    public Color BackgroundHighlightColour { get; set; } = ConsolePalette.White;
    
    public Color BorderColour { get; set; } = ConsolePalette.White;
    
    private void CalculateViewPortBounds()
    {
        int inset = ShowBorder ? 1 : 0;

        int y = ShowColumnHeaders
            ? Y + inset + 1
            : Y + inset;

        viewPort.Bounds = new Rectangle(
            X + inset,
            y,
            Width - inset * 2,
            Height - inset * 2);

        // Computed here rather than lazily in DrawItems() so it - and therefore where the scroll
        // indicators belong - is already correct by the time DrawBorder() runs.
        viewPort.RowCount = viewPort.Bounds.Height - 1;

        if (viewPort.SelectedIndex >= items.Count) {
            viewPort.SelectedIndex = Math.Max(0, items.Count - 1); 
        }
        if (viewPort.PreviousSelectedIndex >= items.Count) {
            viewPort.PreviousSelectedIndex = Math.Max(0, items.Count - 1);
        }
        if (viewPort.CurrentPageIndex > Math.Max(0, items.Count - viewPort.RowCount)) {
            viewPort.CurrentPageIndex = Math.Max(0, items.Count - viewPort.RowCount);
        }
    }
    
    internal void ClearColumnHeaders() => columnHeaders.Clear();

    internal void ClearItems()
    {
        items.Clear();
        viewPort.Reset();
    }

    internal int ColumnHeaderCount => columnHeaders.Count;

    public ListViewColumnHeaderCollection ColumnHeaders => columnHeaderCollection;
    
    internal bool Contains(ListViewColumnHeader columnHeader) =>
        columnHeaders.Contains(columnHeader);
    
    internal bool Contains(ListViewItem item) =>
        items.Contains(item);
    
    private void DoScroll(ConsoleKey consoleKey, out bool redrawAllItems)
    {
        redrawAllItems = false;
        
        switch (consoleKey) {
            case ConsoleKey.DownArrow:
                if (viewPort.SelectedIndex != items.Count - 1) {
                    viewPort.PreviousSelectedIndex = viewPort.SelectedIndex;
                    viewPort.SelectedIndex++;
                    
                    if (viewPort.SelectedIndex - viewPort.CurrentPageIndex >= viewPort.RowCount) {
                        if (viewPort.CurrentPageIndex <= items.Count - viewPort.Bounds.Height + 1) {
                            viewPort.CurrentPageIndex++;
                            redrawAllItems = true;
                        }
                    }
                }
                break;
            
            case ConsoleKey.UpArrow:
                if (viewPort.SelectedIndex != 0) {
                    viewPort.PreviousSelectedIndex = viewPort.SelectedIndex;
                    viewPort.SelectedIndex--;
                    
                    if (viewPort.SelectedIndex <= viewPort.CurrentPageIndex - 1 && viewPort.CurrentPageIndex != 0) {
                        viewPort.CurrentPageIndex--;
                        redrawAllItems = true;
                    }
                }
                else {
                    viewPort.PreviousSelectedIndex = viewPort.SelectedIndex;
                }
                break;
            
            case ConsoleKey.PageDown:
                if (viewPort.SelectedIndex != items.Count - 1) {
                    viewPort.PreviousSelectedIndex = viewPort.SelectedIndex;
                    viewPort.SelectedIndex += viewPort.RowCount;
                    
                    if (viewPort.SelectedIndex > items.Count - 1) {
                        viewPort.SelectedIndex = items.Count - 1;
                    }
                    
                    if (viewPort.SelectedIndex - viewPort.CurrentPageIndex >= viewPort.RowCount) {
                        viewPort.CurrentPageIndex = Math.Min(items.Count - viewPort.RowCount, viewPort.SelectedIndex - viewPort.RowCount + 1);
                        redrawAllItems = true;
                    }
                }
                break;
            
            case ConsoleKey.PageUp:
                if (viewPort.SelectedIndex != 0) {
                    viewPort.PreviousSelectedIndex = viewPort.SelectedIndex;
                    
                    if (viewPort.SelectedIndex > viewPort.RowCount) {
                        viewPort.SelectedIndex -= viewPort.RowCount;
                    }
                    else {
                        viewPort.SelectedIndex = 0;
                    }
                    
                    if (viewPort.SelectedIndex <= viewPort.CurrentPageIndex - 1 && viewPort.CurrentPageIndex != 0) {
                        viewPort.CurrentPageIndex = Math.Max(0, viewPort.SelectedIndex);
                        redrawAllItems = true;
                    }
                }
                break;
        }
    }

    private void DrawBorder()
    {
        int innerWidth = Width - 2;

        // Top: ╭── HeaderText ──╮
        string headerLabel = string.IsNullOrEmpty(HeaderText) ? string.Empty : $" {HeaderText} ";
        int headerLabelLen = Math.Min(headerLabel.Length, innerWidth);
        int headerLeftDashes = (innerWidth - headerLabelLen) / 2;
        int headerRightDashes = innerWidth - headerLabelLen - headerLeftDashes;

        frame.MoveTo(X, Y);
        frame.SetColour(BorderColour, BackgroundColour);
        frame.Append('\u256D');
        frame.Append('\u2500', headerLeftDashes);
        frame.SetColour(ForegroundColour, BackgroundColour);
        frame.Append(headerLabelLen < headerLabel.Length ? headerLabel[..headerLabelLen] : headerLabel);
        frame.SetColour(BorderColour, BackgroundColour);
        frame.Append('\u2500', headerRightDashes);
        frame.Append('\u256E');

        // Left and right sides: │, with the right edge carrying the ▲ / ▼ overscroll glyph at the
        // rows computed up front - written once here rather than drawn plain and overwritten a
        // moment later, which is what caused the flicker.
        (int upRow, int downRow) = GetScrollIndicatorRows();

        for (int row = 1; row < Height - 1; row++) {
            int y = Y + row;

            frame.MoveTo(X, y);
            frame.SetColour(BorderColour, BackgroundColour);
            frame.Append('\u2502');

            frame.MoveTo(X + Width - 1, y);
            frame.Append(y == upRow ? '\u25b2' : y == downRow ? '\u25bc' : '\u2502');
        }

        // Bottom: ╰── FooterText ──╯
        string footerLabel = string.IsNullOrEmpty(FooterText) ? string.Empty : $" {FooterText} ";
        int footerLabelLen = Math.Min(footerLabel.Length, innerWidth);
        int footerLeftDashes = (innerWidth - footerLabelLen) / 2;
        int footerRightDashes = innerWidth - footerLabelLen - footerLeftDashes;

        frame.MoveTo(X, Y + Height - 1);
        frame.SetColour(BorderColour, BackgroundColour);
        frame.Append('\u2570');
        frame.Append('\u2500', footerLeftDashes);
        frame.SetColour(ForegroundColour, BackgroundColour);
        frame.Append(footerLabelLen < footerLabel.Length ? footerLabel[..footerLabelLen] : footerLabel);
        frame.SetColour(BorderColour, BackgroundColour);
        frame.Append('\u2500', footerRightDashes);
        frame.Append('\u256F');
    }

    private void DrawEmptyListView()
    {
        for (int i = 0; i < viewPort.Bounds.Height - 1; i++) {
            frame.MoveTo(viewPort.Bounds.X, viewPort.Bounds.Y + i);
            frame.SetColour(ForegroundColour, BackgroundColour);
            frame.Append(' ', viewPort.Bounds.Width);
        }

        if (string.IsNullOrWhiteSpace(EmptyListViewText)) {
            return;
        }

        if (EmptyListViewText.Length > Width) {
            EmptyListViewText = EmptyListViewText[..Width];
        }

        Point p = new Point((X + (Width - EmptyListViewText.Length)) / 2, Y + (Height / 2));

        frame.MoveTo(p.X, p.Y);
        frame.SetColour(ForegroundColour, BackgroundColour);
        frame.Append(EmptyListViewText);
    }
    
    // Formats text into a fixed-width column cell (width terminal columns, including the trailing
    // separator space). Measures and truncates by terminal display width rather than char count:
    // a column sized in chars would overflow whenever the text contains an East Asian wide
    // character (each renders as two columns), corrupting everything drawn after it in the row.
    internal static string FormatColumnCell(string text, int width, bool rightAligned)
    {
        int contentWidth = width - 1;
        int contentLength = text.TruncateToTerminalWidth(contentWidth, out int contentDisplayWidth);
        int padding = contentWidth - contentDisplayWidth;

        return string.Create(contentLength + padding + 1, (text, contentLength, padding, rightAligned),
            static (span, state) =>
        {
            var (txt, len, pad, rightAlign) = state;
            ReadOnlySpan<char> content = txt.AsSpan(0, len);

            if (rightAlign) {
                span.Slice(0, pad).Fill(' ');
                content.CopyTo(span.Slice(pad));
            }
            else {
                content.CopyTo(span);
                span.Slice(len, pad).Fill(' ');
            }

            span[^1] = ' ';
        });
    }

    // Deliberately not bold: many terminals desaturate a bold foreground colour, which can make it
    // unreadable against certain header background/foreground pairs (e.g. black-on-blue).
    private void DrawHeader()
    {
        frame.MoveTo(viewPort.Bounds.X, viewPort.Bounds.Y - 1);
        frame.SetColour(HeaderForegroundColour, HeaderBackgroundColour);

        if (ColumnHeaderCount == 0) {
            frame.Append(' ', viewPort.Bounds.Width);
            return;
        }

        int c = 0;

        if (ShowCheckboxes) {
            frame.Append(' ', CheckboxWidth);
            c += CheckboxWidth;
        }

        for (int i = 0; i < ColumnHeaderCount; i++) {
            if (columnHeaders[i].Width == 0) {
                continue;
            }
            
            if (c + columnHeaders[i].Width > viewPort.Bounds.Width) {
                break;
            }

            int colWidth = columnHeaders[i].Width;
            var text = columnHeaders[i].Text;
            bool rightAligned = columnHeaders[i].RightAligned;

            string columnStr = FormatColumnCell(text, colWidth, rightAligned);
            
            Color foreground = columnHeaders[i].ForegroundColour ?? HeaderForegroundColour;
            Color background = columnHeaders[i].BackgroundColour ?? HeaderBackgroundColour;

            frame.SetColour(foreground, background);
            frame.Append(columnStr);
            c += colWidth;
        }

        frame.SetColour(HeaderForegroundColour, HeaderBackgroundColour);
        frame.Append(' ', viewPort.Bounds.Width - c);
    }

    private void DrawItem(
        ListViewItem item,
        int top,
        bool highlight)
    {
        frame.MoveTo(viewPort.Bounds.X, top);

        bool selected = highlight && EnableRowSelect;
        (Color selectionForeground, Color selectionBackground) = SelectionColours();

        int c = 0;

        if (ShowCheckboxes) {
            frame.SetColour(
                selected ? selectionForeground : ForegroundColour,
                selected ? selectionBackground : BackgroundColour);
            frame.Append(item.Checked ? CheckedText : UnCheckedText);
            c += CheckboxWidth;
        }

        for (int i = 0; i < item.SubItemCount; i++) {
            if (i < ColumnHeaderCount && columnHeaders[i].Width == 0) {
                continue;
            }

            ListViewSubItem subItem = item.SubItems[i];

            bool rightAligned = false;
            int columnWidth = DefaultColumnWidth;

            if (i < ColumnHeaderCount) {
                rightAligned = columnHeaders[i].RightAligned;
                columnWidth = columnHeaders[i].Width;
            }

            if (c + columnWidth > viewPort.Bounds.Width) {
                break;
            }

            string columnStr = FormatColumnCell(subItem.Text, columnWidth, rightAligned);
            
            // A cell that set its own background (a heat / severity colour) keeps its own colours
            // through the selection band. Cells left at the list's default background, and the
            // filler past the last column, take the highlight so the selected row still reads as
            // one strip.
            bool cellHasCustomBackground = !SameColour(subItem.BackgroundColor, BackgroundColour);

            Color foregroundColour;
            Color backgroundColour;

            if (selected && !cellHasCustomBackground) {
                foregroundColour = selectionForeground;
                backgroundColour = selectionBackground;
            }
            else {
                foregroundColour = subItem.ForegroundColor;
                backgroundColour = subItem.BackgroundColor;
            }

            frame.SetColour(foregroundColour, backgroundColour);
            frame.Append(columnStr);
            c += columnWidth;
        }

        if (selected) {
            frame.SetColour(selectionForeground, selectionBackground);
        }
        else {
            frame.SetColour(ForegroundColour, item.SubItems[item.SubItemCount - 1].BackgroundColor);
        }

        frame.Append(' ', viewPort.Bounds.Width - c);
    }

    private void DrawItems()
    {
        int n = 0;

        for (int i = 0; i < viewPort.RowCount; i++) {
            int pos = i + viewPort.CurrentPageIndex;

            if (pos < ItemCount) {
                ListViewItem item = Items[pos];
                DrawItem(item, viewPort.Bounds.Y + n, highlight: pos == viewPort.SelectedIndex);
                n++;
            }
        }

        for (int i = n; i < viewPort.RowCount; i++) {
            frame.MoveTo(viewPort.Bounds.X, viewPort.Bounds.Y + i);
            frame.SetColour(ForegroundColour, BackgroundColour);
            frame.Append(' ', viewPort.Bounds.Width);
        }
    }

    // The screen row for the up/down overscroll glyph, or -1 when that direction has nothing
    // further to scroll to (plain border character there instead). Shared by DrawBorder(), which
    // paints the border's side rows in a single pass, and DrawScrollIndicators() below, which
    // repaints only these two cells on the key-driven partial redraw that doesn't touch the border
    // at all - so the two draw paths can never disagree with each other.
    private (int UpRow, int DownRow) GetScrollIndicatorRows()
    {
        if (!EnableScroll || viewPort.RowCount <= 0 || ItemCount <= viewPort.RowCount) {
            return (-1, -1);
        }

        int upRow = viewPort.CurrentPageIndex > 0
            ? viewPort.Bounds.Y
            : -1;

        int downRow = viewPort.CurrentPageIndex + viewPort.RowCount < ItemCount
            ? viewPort.Bounds.Y + viewPort.RowCount - 1
            : -1;

        return (upRow, downRow);
    }

    // Repaints just the two indicator cells: used only by the arrow/PageUp/PageDown partial
    // redraw, where the border itself is left untouched. A full redraw never calls this - DrawBorder()
    // already paints the correct character there in its one pass over the side rows, so writing the
    // plain character first and overwriting it here a moment later (the previous approach) doesn't
    // happen anymore; that double-write was what caused the flicker.
    private void DrawScrollIndicators()
    {
        if (!ShowBorder || viewPort.RowCount <= 0) {
            return;
        }

        (int upRow, int downRow) = GetScrollIndicatorRows();

        int x = X + Width - 1;
        int topRow = viewPort.Bounds.Y;
        int bottomRow = viewPort.Bounds.Y + viewPort.RowCount - 1;

        frame.MoveTo(x, topRow);
        frame.SetColour(BorderColour, BackgroundColour);
        frame.Append(topRow == upRow ? '▲' : '│');

        if (bottomRow != topRow) {
            frame.MoveTo(x, bottomRow);
            frame.SetColour(BorderColour, BackgroundColour);
            frame.Append(bottomRow == downRow ? '▼' : '│');
        }
    }

    public string EmptyListViewText { get; set; } = string.Empty;
    
    public bool EnableRowSelect { get; set; }
    
    public bool EnableScroll { get; set; }
    
    public Color ForegroundHighlightColour { get; set; } = ConsolePalette.Cyan;

    internal ListViewColumnHeader GetColumnHeaderByIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, columnHeaders.Count, nameof(index));
        
        return columnHeaders[index];
    }

    private void FrameClear() =>
        frame.Clear();

    private void FrameWrite()
    {
        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }
    
    internal ListViewItem GetItemByIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, items.Count, nameof(index));
        
        return items[index];
    }

    public Color HeaderBackgroundColour { get; set; } = ConsolePalette.Black;
    
    public Color HeaderForegroundColour { get; set; } = ConsolePalette.White;
    
    internal int IndexOfColumnHeader(ListViewColumnHeader columnHeader)
    {
        for (int i = 0; i < columnHeaders.Count; i++) {
            if (columnHeaders[i] == columnHeader) {
                return i;
            }
        }

        return -1;
    }
    
    internal int IndexOfItem(ListViewItem item)
    {
        for (int i = 0; i < items.Count; i++) {
            if (items[i] == item) {
                return i;
            }
        }

        return -1;
    }

    internal void InsertColumnHeader(int index, ListViewColumnHeader columnHeader)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, items.Count, nameof(index));
        
        columnHeaders.Insert(index, columnHeader);
    }

    internal void InsertColumnHeaders(ListViewColumnHeader[] columnHeaders) => 
        this.columnHeaders.AddRange(columnHeaders);
    
    internal void InsertItem(int index, ListViewItem item)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, items.Count, nameof(index));

        item.Parent = this;
        items.Insert(index, item);
    }

    internal void InsertItems(ListViewItem[] items)
    {
        for (int i = 0; i < items.Length; i++) {
            items[i].Parent = this;
        }
        
        this.items.AddRange(items);
    }
    
    internal int ItemCount => items.Count;
    
    public ListViewItemCollection Items => itemCollection;
    
    protected override void OnDraw()
    {
        FrameClear();
        CalculateViewPortBounds();

        if (ShowBorder) {
            DrawBorder();
        }

        if (ShowColumnHeaders) {
            DrawHeader();
        }

        if (Items.Count > 0) {
            // The right border already carries the correct ▲ / ▼ glyph from DrawBorder() above -
            // no separate indicator pass needed (and none wanted; see DrawScrollIndicators()).
            DrawItems();
        }
        else {
            DrawEmptyListView();
        }

        FrameWrite();
    }

    protected void OnItemClicked(ListViewItem item) =>
        ItemClicked?.Invoke(this, new ListViewItemEventArgs(item));

    protected void OnItemSelected(ListViewItem item) =>
        ItemSelected?.Invoke(this, new ListViewItemEventArgs(item));

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        if (Items.Count == 0) {
            return;
        }

        switch (keyInfo.Key) {
            case ConsoleKey.UpArrow:
            case ConsoleKey.DownArrow:
            case ConsoleKey.PageUp:
            case ConsoleKey.PageDown: {
                if (!EnableScroll) {
                    return;
                }

                DoScroll(keyInfo.Key, out bool redrawAllItems);

                if (redrawAllItems) {
                    frame.Clear();
                    DrawHeader();
                    DrawItems();
                    DrawScrollIndicators();
                    frame.ResetColour();
                    Terminal.Write(frame.AsSpan());
                }
                else {
                    RedrawItem();
                }

                if (SelectedIndex != -1) {
                    OnItemClicked(SelectedItem!);
                }

                handled = true;
                break;
            }
            case ConsoleKey.Enter: {

                if (SelectedIndex != -1) {
                    OnItemSelected(SelectedItem!);
                }

                handled = true;
                break;
            }
            case ConsoleKey.Spacebar: {
                if (!ShowCheckboxes) {
                    return;
                }

                if (SelectedIndex != -1) {
                    SelectItemCheckbox(SelectedItem!);
                    RedrawItem();
                }

                handled = true;
                break;
            }
        }
    }

    protected override void OnResize() => CalculateViewPortBounds();

    private void RedrawItem()
    {
        frame.Clear();
        ListViewItem selectedItem = items[viewPort.SelectedIndex];
        
        DrawItem(
            selectedItem,
            viewPort.Bounds.Y + viewPort.SelectedIndex - viewPort.CurrentPageIndex,
            highlight: true);

        if (viewPort.PreviousSelectedIndex != viewPort.SelectedIndex) {
            ListViewItem previousSelectedItem = items[viewPort.PreviousSelectedIndex];
            DrawItem(
                previousSelectedItem,
                viewPort.Bounds.Y + viewPort.PreviousSelectedIndex - viewPort.CurrentPageIndex,
                highlight: false);
        }

        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }
    
    internal void RemoveAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, items.Count, nameof(index));
        
        items.RemoveAt(index);
    }

    internal void RemoveItem(ListViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item, nameof(item));
        int index = IndexOfItem(item);
        
        if (index != -1) {
            RemoveAt(index);
        }
    }

    public int SelectedIndex
    {
        get => viewPort.SelectedIndex;
        set {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(value));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, items.Count, nameof(value));

            viewPort.SelectedIndex = value;
        }
    }

    public ListViewItem? SelectedItem
    {
        get {
            if (items.Count == 0) {
                return null;
            }
            
            return items[SelectedIndex];
        }
    }

    private void SelectItemCheckbox(ListViewItem item) => item.Checked = !item.Checked;

    // The colours a selected row is drawn in: the configured highlight pair when the control has
    // focus, otherwise the muted black-on-gray used for an unfocused selection.
    private (Color Foreground, Color Background) SelectionColours() =>
        Focused
            ? (ForegroundHighlightColour, BackgroundHighlightColour)
            : (ConsolePalette.Black, ConsolePalette.Gray);

    private static bool SameColour(Color left, Color right) => left.ToArgb() == right.ToArgb();

    public bool ShowBorder { get; set; }

    public bool ShowCheckboxes { get; set; }

    public bool ShowColumnHeaders { get; set; }

    public string FooterText { get; set; } = string.Empty;

    public string HeaderText { get; set; } = string.Empty;
}
