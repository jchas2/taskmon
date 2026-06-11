using Task.Monitor.System.Controls.ListView;

namespace Task.Monitor.System.Tests.Controls.ListView;

public sealed class ListViewColumnHeaderTests
{
    [Fact]
    public void Constructor_With_Text_Initialises_Correctly()
    {
        string headerText = "Test Header";
        ListViewColumnHeader header = new(headerText);
        
        Assert.Equal(headerText, header.Text);
        Assert.Null(header.BackgroundColour);
        Assert.Null(header.ForegroundColour);
        Assert.False(header.RightAligned);
    }

    [Fact]
    public void Constructor_Accepts_Empty_Text()
    {
        ListViewColumnHeader header = new(string.Empty);
        Assert.True(header.Text == string.Empty);
    }

    [Fact]
    public void Constructor_With_All_Parameters_Initialises_Correctly()
    {
        string headerText = "Full Header";
        ConsoleColor bgColor = ConsoleColor.Blue;
        ConsoleColor fgColor = ConsoleColor.Yellow;
        var header = new ListViewColumnHeader(headerText, bgColor, fgColor);
        
        Assert.Equal(headerText, header.Text);
        Assert.True(bgColor == header.BackgroundColour);
        Assert.True(fgColor == header.ForegroundColour);
        Assert.False(header.RightAligned);
    }
    
    [Fact]
    public void Constructor_With_All_Parameters_Accepts_Empty_Text()
    {
        ConsoleColor bgColor = ConsoleColor.Blue;
        ConsoleColor fgColor = ConsoleColor.Yellow;

        ListViewColumnHeader header = new(string.Empty, bgColor, fgColor);
        Assert.True(header.Text == String.Empty);
    }    
}
