using Task.Monitor.System.Controls.ListView;

namespace Task.Monitor.System.Tests.Controls.ListView;

public sealed class ListViewSubItemCollectionTests
{
    private readonly ListViewItem item;

    public ListViewSubItemCollectionTests() =>
        item = new("Item 0");

    [Fact]
    public void Should_Add_SubItem()
    {
        item.SubItems.Add(new ListViewSubItem(item, "SubItem 1"));
        
        // Count should be 2 as the ListViewItem adds 1 SubItem by default for the initial text.
        Assert.True(2 == item.SubItems.Count());
    }

    [Fact]
    public void Should_Clear_Collection()
    {
        item.SubItems.Add(new ListViewSubItem(item, "SubItem 1"));
        item.SubItems.Clear();
        
        Assert.True(0 == item.SubItems.Count());
    }

    [Fact]
    public void Should_Enumerate_Collection()
    {
        item.SubItems.Add(new ListViewSubItem(item, "SubItem 1"));
        item.SubItems.Add(new ListViewSubItem(item, "SubItem 2"));
        
        Assert.True(3 == item.SubItems.Count());

        foreach (ListViewSubItem subItem in item.SubItems) {
            Assert.NotNull(subItem);
        }
    }
}