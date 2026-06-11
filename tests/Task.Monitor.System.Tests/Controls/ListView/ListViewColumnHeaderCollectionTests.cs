using Moq;
using Task.Monitor.System.Controls.ListView;
using ListViewControl = Task.Monitor.System.Controls.ListView.ListView;

namespace Task.Monitor.System.Tests.Controls.ListView;

public sealed class ListViewColumnHeaderCollectionTests
{
    private readonly Mock<ISystemTerminal> terminal;
    private readonly ListViewControl listView;
    private readonly ListViewColumnHeaderCollection collection;

    public ListViewColumnHeaderCollectionTests()
    {
        terminal = TerminalMock.Setup();
        listView = new ListViewControl(terminal.Object);
        collection = new ListViewColumnHeaderCollection(listView);
    }

    [Fact]
    public void Should_Add_ColumnHeader()
    {
        collection.Add(new ListViewColumnHeader("Header 1"));
        
        Assert.True(1 == collection.Count());
    }

    [Fact]
    public void Should_Clear_Collection()
    {
        collection.Add(new ListViewColumnHeader("Header 1"));
        collection.Clear();
        
        Assert.True(0 == collection.Count());
    }

    [Fact]
    public void Should_Enumerate_Collection()
    {
        collection.Add(new ListViewColumnHeader("Header 1"));
        collection.Add(new ListViewColumnHeader("Header 2"));

        foreach (ListViewColumnHeader colHeader in collection) {
            Assert.NotNull(colHeader);
        }
    }
}
