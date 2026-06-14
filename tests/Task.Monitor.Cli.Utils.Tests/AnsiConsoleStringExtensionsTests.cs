namespace Task.Monitor.Cli.Utils.Tests;

public sealed class AnsiConsoleStringExtensionsTests
{
    private const char Esc = (char)27;

    [Theory]
    [InlineData(ConsoleColor.Black, "[30m")]
    [InlineData(ConsoleColor.DarkGreen, "[32m")]
    [InlineData(ConsoleColor.Gray, "[37m")]
    [InlineData(ConsoleColor.Green, "[92m")]
    [InlineData(ConsoleColor.Red, "[91m")]
    [InlineData(ConsoleColor.Yellow, "[93m")]
    [InlineData(ConsoleColor.White, "[97m")]
    public void GetForegroundCode_Returns_Expected_Sgr(ConsoleColor colour, string expectedTail)
    {
        Assert.Equal(Esc + expectedTail, AnsiConsoleStringExtensions.GetForegroundCode(colour).ToString());
    }

    [Theory]
    [InlineData(ConsoleColor.Black, "[40m")]
    [InlineData(ConsoleColor.DarkGreen, "[42m")]
    [InlineData(ConsoleColor.Green, "[102m")]
    [InlineData(ConsoleColor.Red, "[101m")]
    [InlineData(ConsoleColor.Yellow, "[103m")]
    [InlineData(ConsoleColor.White, "[107m")]
    public void GetBackgroundCode_Returns_Expected_Sgr(ConsoleColor colour, string expectedTail)
    {
        Assert.Equal(Esc + expectedTail, AnsiConsoleStringExtensions.GetBackgroundCode(colour).ToString());
    }

    [Fact]
    public void Reset_Is_The_Sgr_Reset_Sequence()
    {
        string reset = AnsiConsoleStringExtensions.Reset;
        Assert.Equal(Esc + "[0m", reset);
    }

    [Fact]
    public void All_ConsoleColors_Have_A_Code()
    {
        foreach (ConsoleColor colour in Enum.GetValues<ConsoleColor>()) {
            Assert.False(AnsiConsoleStringExtensions.GetForegroundCode(colour).IsEmpty);
            Assert.False(AnsiConsoleStringExtensions.GetBackgroundCode(colour).IsEmpty);
        }
    }
}
