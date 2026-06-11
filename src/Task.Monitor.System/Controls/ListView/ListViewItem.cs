namespace Task.Monitor.System.Controls.ListView;

public class ListViewItem
{
    //
    // The Collection acts as a proxy for updates to the underlying List<T>.
    // This provides a clean api for interacting with Collections on the ListViewItem
    // control, similar to the Win32 ListView common control.
    // 
    private readonly ListViewSubItemCollection subItemCollection;

    // The containers holding the List<T> for rendering. We don't expose them via a public api.
    private List<ListViewSubItem> subItems = [];
    
    public ListViewItem(string text)
    {
        subItems.Add(new ListViewSubItem(this, text));
        subItemCollection = new ListViewSubItemCollection(this);
    }

    public ListViewItem(
        string text,
        ConsoleColor backgroundColor,
        ConsoleColor foregroundColor)
        : this(text)
    {
        BackgroundColour = backgroundColor;
        ForegroundColour = foregroundColor;
    }
    
    public ListViewItem(string[] items)
    {
        for (int i = 0; i < items.Length; i++) {
            ArgumentNullException.ThrowIfNull(items[i], nameof(items));
            subItems.Add(new ListViewSubItem(this, items[i]));
        }
        
        subItemCollection = new ListViewSubItemCollection(this);
    }

    public ListViewItem(
        string[] items,
        ConsoleColor backgroundColor,
        ConsoleColor foregroundColor)
        : this(items)
    {
        BackgroundColour = backgroundColor;
        ForegroundColour = foregroundColor;
    }

    public ListViewItem(ListViewSubItem[] subItems)
    {
        for (int i = 0; i < subItems.Length; i++) {
            ArgumentNullException.ThrowIfNull(subItems[i], nameof(subItems));
            subItems[i].Owner = this;
            this.subItems.Add(subItems[i]);
        }
        
        subItemCollection = new ListViewSubItemCollection(this);
    }

    public ListViewItem(
        ListViewSubItem[] subItems,
        ConsoleColor backgroundColor,
        ConsoleColor foregroundColor)
        : this(subItems)
    {
        BackgroundColour = backgroundColor;
        ForegroundColour = foregroundColor;
    }

    public ConsoleColor BackgroundColour
    {
        get {
            if (SubItemCount != 0) {
                return SubItems[0].BackgroundColor;
            }
            
            return Parent?.BackgroundColour ?? ConsoleColor.Black;
        }
        set => SubItems[0].BackgroundColor = value;
    }

    public bool Checked { get; set; }
    
    internal void ClearSubItems() => subItems.Clear();

    internal bool Contains(ListViewSubItem subItem) =>
        subItems.Contains(subItem);

    public ConsoleColor ForegroundColour 
    {
        get {
            if (SubItemCount != 0) {
                return SubItems[0].ForegroundColor;
            }
            
            return Parent?.ForegroundColour ?? ConsoleColor.White;
        }
        set => SubItems[0].ForegroundColor = value;
    }
    

    internal ListViewSubItem GetSubItemByIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, subItems.Count, nameof(index));
        
        return subItems[index];
    }
    
    internal int IndexOfSubItem(ListViewSubItem subItem)
    {
        for (int i = 0; i < subItems.Count; i++) {
            if (subItems[i] == subItem) {
                return i;
            }
        }

        return -1;
    }

    internal void InsertSubItems(ListViewSubItem[] subItems) =>
        this.subItems.AddRange(subItems);

    internal ListView? Parent { get; set; }
    
    internal int SubItemCount => subItems.Count;
    
    public ListViewSubItemCollection SubItems => subItemCollection;
    
    public string Text
    {
        get => subItems[0].Text;
        set => subItems[0].Text = value;
    }
}
