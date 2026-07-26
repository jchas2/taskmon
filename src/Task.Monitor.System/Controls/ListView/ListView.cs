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
        ShowColumnHeaders = true; 
    }
    
    public Color BackgroundHighlightColour { get; set; } = ConsolePalette.White;

    private void CalculateViewPortBounds()
    {
        // Bounds is the scrollable region for the ListViewItems. The value 1
        // is added to make room for the header.
        int y = ShowColumnHeaders 
            ? Y + 1 
            : Y;
        
        viewPort.Bounds = new Rectangle(X, y, Width, Height);
        
        if (viewPort.SelectedIndex >= items.Count) {
            viewPort.SelectedIndex = items.Count - 1;
        }
        if (viewPort.PreviousSelectedIndex >= items.Count) {
            viewPort.PreviousSelectedIndex = items.Count - 1;
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

    private void DrawEmptyListView()
    {
        for (int i = 0; i < Height - 1; i++) {
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
        
            string columnStr = string.Create(colWidth, (text, colWidth, rightAligned), static (span, state) =>
            {
                var (txt, width, rightAlign) = state;
                int contentWidth = width - 1;

                // Truncate if needed.
                ReadOnlySpan<char> content = txt.Length >= width 
                    ? txt.AsSpan(0, width - 1)
                    : txt.AsSpan();

                if (rightAlign) {
                    int padding = contentWidth - content.Length;
                    span.Slice(0, padding).Fill(' ');
                    content.CopyTo(span.Slice(padding));
                }
                else {
                    content.CopyTo(span);
                    span.Slice(content.Length, contentWidth - content.Length).Fill(' ');
                }

                span[width - 1] = ' ';
            });
            
            Color foreground = columnHeaders[i].ForegroundColour ?? HeaderForegroundColour;
            Color background = columnHeaders[i].BackgroundColour ?? HeaderBackgroundColour;

            frame.SetColour(foreground, background);
            frame.SetBold(true);
            frame.Append(columnStr);
            c += colWidth;
        }

        frame.SetBold(false);
        frame.SetColour(HeaderForegroundColour, HeaderBackgroundColour);
        frame.Append(' ', viewPort.Bounds.Width - c);
    }

    private void DrawItem(
        ListViewItem item,
        int top,
        bool highlight)
    {
        frame.MoveTo(viewPort.Bounds.X, top);

        int c = 0;

        if (ShowCheckboxes) {
            frame.SetColour(ForegroundColour, BackgroundColour);
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

            string columnStr = string.Create(columnWidth, (subItem.Text, columnWidth, rightAligned), 
                static (span, state) =>
            {
                var (text, width, rightAlign) = state;
                int contentWidth = width - 1;

                // Truncate if needed
                ReadOnlySpan<char> content = text.Length >= width 
                    ? text.AsSpan(0, width - 1)
                    : text.AsSpan();

                if (rightAlign) {
                    int padding = contentWidth - content.Length;
                    span.Slice(0, padding).Fill(' ');
                    content.CopyTo(span.Slice(padding));
                }
                else {
                    content.CopyTo(span);
                    span.Slice(content.Length, contentWidth - content.Length).Fill(' ');
                }

                span[width - 1] = ' ';
            });
            
            bool selected = highlight && EnableRowSelect;

            Color foregroundColour = selected
                ? Focused 
                    ? ForegroundHighlightColour 
                    : ConsolePalette.Black
                : subItem.ForegroundColor;

            Color backgroundColour = selected
                ? Focused 
                    ? BackgroundHighlightColour 
                    : ConsolePalette.Gray
                : subItem.BackgroundColor;
            
            frame.SetColour(foregroundColour, backgroundColour);
            frame.Append(columnStr);
            c += columnWidth;
        }

        frame.SetColour(ForegroundColour, item.SubItems[item.SubItemCount - 1].BackgroundColor);
        frame.Append(' ', viewPort.Bounds.Width - c);
    }

    private void DrawItems()
    {
        viewPort.RowCount = viewPort.Bounds.Height - 1;

        int n = 0;

        for (int i = 0; i < viewPort.RowCount; i++) {
            int pos = i + viewPort.CurrentPageIndex;

            if (pos < ItemCount) {
                ListViewItem item = Items[pos];
                DrawItem(item, viewPort.Bounds.Y + n, highlight: pos == viewPort.SelectedIndex);
                n++;
            }
        }

        for (int i = n; i < Height - 1; i++) {
            frame.MoveTo(viewPort.Bounds.X, viewPort.Bounds.Y + i);
            frame.SetColour(ForegroundColour, BackgroundColour);
            frame.Append(' ', viewPort.Bounds.Width);
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
        
        if (ShowColumnHeaders) {
            DrawHeader();
        }

        if (Items.Count > 0) {
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
    
    public bool ShowCheckboxes { get; set; }
    
    public bool ShowColumnHeaders { get; set; }
}
