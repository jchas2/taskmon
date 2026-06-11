using System.Collections;

namespace Task.Monitor.System.Controls.ListView;

public sealed class ListViewItemCollection : IEnumerable<ListViewItem>
{
    private readonly ListView owner;
    
    public ListViewItemCollection(ListView owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    
    public void Add(ListViewItem item) =>
        owner.InsertItems([item]);

    public void AddRange(params ListViewItem[] items) =>
        owner.InsertItems(items);

    public void Clear() => owner.ClearItems();

    public bool Contains(ListViewItem item) =>
        owner.Contains(item);

    public int Count => owner.ItemCount;

    public IEnumerator<ListViewItem> GetEnumerator()
    {
        // Shallow copy the items and return an enumerator off that container.
        List<ListViewItem> items = new(owner.Items.Count);
        
        for (int i = 0; i < owner.ItemCount; i++) {
            items.Add(owner.GetItemByIndex(i));
        }
        
        return items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int IndexOf(ListViewItem item) =>
        owner.IndexOfItem(item);

    public void InsertAt(int index, ListViewItem item)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, owner.Items.Count, nameof(index));
        
        owner.InsertItem(index, item);
    }
    
    public void Remove(ListViewItem item) =>
        owner.RemoveItem(item);

    public void RemoveAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, owner.Items.Count - 1, nameof(index));
        
        owner.RemoveAt(index);
    }
    
    public ListViewItem this[int index]
    {
        get {
            ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
            return owner.GetItemByIndex(index);
        }
        set {
            ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
            owner.RemoveAt(index);
            owner.InsertItem(index, value);
        }
    }
}
