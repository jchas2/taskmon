using System.Collections;

namespace Task.Monitor.System.Controls.ListView;

public class ListViewColumnHeaderCollection : IEnumerable<ListViewColumnHeader>
{
    private readonly ListView owner;
    
    public ListViewColumnHeaderCollection(ListView owner) =>
        this.owner = owner;
    
    public ListViewColumnHeaderCollection Add(ListViewColumnHeader columnHeader)
    {
        owner.InsertColumnHeaders([columnHeader]);
        return this;
    }

    public void AddRange(params ListViewColumnHeader[] columnHeaders) =>
        owner.InsertColumnHeaders(columnHeaders);

    public void Clear() => owner.ClearColumnHeaders();

    public bool Contains(ListViewColumnHeader columnHeader) =>
        owner.Contains(columnHeader);

    public IEnumerator<ListViewColumnHeader> GetEnumerator()
    {
        // Shallow copy the items and return an enumerator off that container.
        List<ListViewColumnHeader> columnHeaders = new(owner.ColumnHeaderCount);
        
        for (int i = 0; i < owner.ColumnHeaderCount; i++) {
            columnHeaders.Add(owner.GetColumnHeaderByIndex(i));
        }
        
        return columnHeaders.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int IndexOf(ListViewColumnHeader columnHeader) =>
        owner.IndexOfColumnHeader(columnHeader);
    
    public ListViewColumnHeader this[int index]
    {
        get {
            ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
            return owner.GetColumnHeaderByIndex(index);
        }
        set => throw new InvalidOperationException();
    }
}