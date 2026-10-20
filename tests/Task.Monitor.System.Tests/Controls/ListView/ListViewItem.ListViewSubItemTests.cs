using System.Drawing;
using Moq;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls.ListView;
using ListViewControl = Task.Monitor.System.Controls.ListView.ListView;
namespace Task.Monitor.System.Tests.Controls.ListView;

public sealed class ListViewSubItemTests
{
    [Fact]
    public void Constructor_With_All_Parameters_Initialises_Correctly()
    {
        ListViewItem item = new("Item");
        
        ListViewSubItem subItem = new(
            item,
            "Sub Item", 
            ConsolePalette.Green, 
            ConsolePalette.Black);
        
        Assert.Equal("Sub Item", subItem.Text);
        Assert.True(ConsolePalette.Green == subItem.BackgroundColor);
        Assert.True(ConsolePalette.Black == subItem.ForegroundColor);
    }

    [Fact]
    public void Constructor_With_Text_Initialises_Correctly()
    {
        ListViewItem item = new("Item");
        ListViewSubItem subItem = new(item, "Sub Item");
        
        Assert.Equal("Sub Item", subItem.Text);
        Assert.True(ConsolePalette.Black == subItem.BackgroundColor);
        Assert.True(ConsolePalette.White == subItem.ForegroundColor);
    }
}

