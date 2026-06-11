using Task.Monitor.System.Controls.ListView;

namespace Task.Monitor.System.Tests.Controls.ListView;

public sealed class ListViewItemTests
{
    [Fact]
    public void Constructor_With_Text_Initialises_Correctly()
    {
        ListViewItem item = new ListViewItem("Item");
        
        Assert.Equal("Item", item.Text);
    }
    
    [Fact]
    public void Constructor_With_Text_And_Colours_Initialises_Correctly()
    {
        ListViewItem item = new ListViewItem("Item", ConsoleColor.Green, ConsoleColor.Yellow);
        
        Assert.Equal("Item", item.Text);
        Assert.Equal(ConsoleColor.Green, item.BackgroundColour);
        Assert.Equal(ConsoleColor.Yellow, item.ForegroundColour);
    }
    
    [Fact]
    public void Constructor_With_Text_Array_Initialises_Correctly()
    {
        ListViewItem item = new ListViewItem(new[] { "Apples", "Oranges", "Bananas" });
        
        Assert.Equal("Apples", item.Text);
        Assert.Equal("Apples", item.SubItems[0].Text);
        Assert.Equal("Oranges", item.SubItems[1].Text);
        Assert.Equal("Bananas", item.SubItems[2].Text);
    }

    [Fact]
    public void Constructor_With_Text_Array_And_Colours_Initialises_Correctly()
    {
        ListViewItem item = new ListViewItem(
            new[] { "Apples", "Oranges", "Bananas" },
            ConsoleColor.Green,
            ConsoleColor.Yellow);
        
        Assert.Equal("Apples", item.Text);
        Assert.Equal("Apples", item.SubItems[0].Text);
        Assert.Equal("Oranges", item.SubItems[1].Text);
        Assert.Equal("Bananas", item.SubItems[2].Text);
        Assert.Equal(ConsoleColor.Green, item.BackgroundColour);
        Assert.Equal(ConsoleColor.Yellow, item.ForegroundColour);
    }
}

