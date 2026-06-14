using Task.Monitor.System.Controls.ListView;

namespace Task.Monitor.System.Tests.Controls.ListView;

public sealed class ListViewItemEventArgsTests
{
    [Fact]
    public void ListViewItemEventArgs_Ctor()
    {
        ListViewItem item = new("Test");
        ListViewItemEventArgs args = new(item);
        
        Assert.Equal(item, args.Item);
    }
}