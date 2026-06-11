using System.Collections;

namespace Task.Monitor.System.Controls.ListView;

public class ListViewSubItemCollection : IEnumerable<ListViewSubItem>
{
    private readonly ListViewItem owner;
    
    public ListViewSubItemCollection(ListViewItem owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    
    public void Add(ListViewSubItem subItem) =>
        owner.InsertSubItems([subItem]);

    public void AddRange(params ListViewSubItem[] subItems) =>
        owner.InsertSubItems(subItems);

    public void Clear() => owner.ClearSubItems();

    public bool Contains(ListViewSubItem subItem) =>
        owner.Contains(subItem);

    public IEnumerator<ListViewSubItem> GetEnumerator()
    {
        // Shallow copy the subitems and return an enumerator off that container.
        List<ListViewSubItem> subItems = new(owner.SubItemCount);
        
        for (int i = 0; i < owner.SubItemCount; i++) {
            subItems.Add(owner.GetSubItemByIndex(i));
        }
        
        return subItems.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int IndexOf(ListViewSubItem subItem) =>
        owner.IndexOfSubItem(subItem);
    
    public ListViewSubItem this[int index]
    {
        get {
            ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
            return owner.GetSubItemByIndex(index);
        }
        set => throw new InvalidOperationException();
    }
}
