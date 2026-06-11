using Moq;
using Task.Monitor.System.Controls.ListView;
using ListViewControl = Task.Monitor.System.Controls.ListView.ListView;

namespace Task.Monitor.System.Tests.Controls.ListView;

public class ListViewItemCollectionTests
{
    private readonly Mock<ISystemTerminal> terminalMock = new();
    
    public static List<ListViewItem> GetListViewItemData()
        => new() {
            new ListViewItem("Item 1"),
            new ListViewItem("Item 2"),
            new ListViewItem("Item 3")
        };

    [Fact]
    public void Should_Add_Items()
    {
        ListViewControl listview = new(terminalMock.Object);

        foreach (ListViewItem item in GetListViewItemData()) {
            listview.Items.Add(item);
        }
        
        Assert.True(listview.Items.Count == 3);
        Assert.True(listview.Items[0].Text == "Item 1");
        Assert.True(listview.Items[1].Text == "Item 2");
        Assert.True(listview.Items[2].Text == "Item 3");
    }
    
    [Fact]
    public void Should_AddRange_Items()
    {
        ListViewControl listview = new(terminalMock.Object);
        listview.Items.AddRange(GetListViewItemData().ToArray());
        
        Assert.True(listview.Items.Count == 3);
        Assert.True(listview.Items[0].Text == "Item 1");
        Assert.True(listview.Items[1].Text == "Item 2");
        Assert.True(listview.Items[2].Text == "Item 3");
    }

    [Fact]
    public void Should_Clear_Items()
    {
        ListViewControl listview = new(terminalMock.Object);
        listview.Items.AddRange(GetListViewItemData().ToArray());
        
        Assert.True(listview.Items.Count == 3);
        
        listview.Items.Clear();
        
        Assert.True(listview.Items.Count == 0);
    }
    
    [Fact]
    public void Should_Enumerate_Items()
    {
        ListViewControl listview = new(terminalMock.Object);
        listview.Items.AddRange(GetListViewItemData().ToArray());

        foreach (var item in listview.Items) {
            Assert.NotNull(item);
        }
    }

    [Fact]
    public void Should_Get_Selected_Item()
    {
        ListViewControl listview = new(terminalMock.Object);
        listview.Items.AddRange(GetListViewItemData().ToArray());
        
        Assert.NotNull(listview.SelectedItem);
        Assert.True(listview.SelectedItem.Text == "Item 1");
    }
    
}