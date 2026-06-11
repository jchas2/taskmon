namespace Task.Monitor.Cli.Utils.Tests;

public sealed class ConsoleColorUtilsTests
{
    public static TheoryData<string, ConsoleColor> ColourData()
        => new()
        {
            { "Black", ConsoleColor.Black },
            { "Blue", ConsoleColor.Blue },
            { "Cyan", ConsoleColor.Cyan },
            { "Gray", ConsoleColor.Gray },
            { "Green", ConsoleColor.Green },
            { "Magenta", ConsoleColor.Magenta },
            { "Red", ConsoleColor.Red },
            { "White", ConsoleColor.White },
            { "Yellow", ConsoleColor.Yellow },
            { "DarkBlue", ConsoleColor.DarkBlue },
            { "DarkCyan", ConsoleColor.DarkCyan },
            { "DarkGreen", ConsoleColor.DarkGreen },
            { "DarkGray", ConsoleColor.DarkGray },
            { "DarkMagenta", ConsoleColor.DarkMagenta },
            { "DarkRed", ConsoleColor.DarkRed },
            { "DarkYellow", ConsoleColor.DarkYellow }
        };
    
    [Theory]
    [MemberData(nameof(ColourData))]
    public void Should_Get_Colour_FromName(string name, ConsoleColor expected)
    {
        var colour = ConsoleColorUtils.FromName(
            name, name != "Black" 
                ? ConsoleColor.Black 
                : ConsoleColor.White);
        
        Assert.Equal(expected, colour);
    }
}
