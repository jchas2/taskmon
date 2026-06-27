using System.Drawing;

namespace Task.Monitor.Cli.Utils.Tests;

public sealed class AnsiConsoleStringExtensionsTests
{
    private const char Esc = (char)27;

    [Fact]
    public void Reset_Is_The_Sgr_Reset_Sequence()
    {
        string reset = AnsiConsoleStringExtensions.Reset;
        Assert.Equal(Esc + "[0m", reset);
    }

    [Fact]
    public void ToColour_Wraps_Text_With_Background_Foreground_And_Reset()
    {
        Color fg = Color.FromArgb(255, 10, 20, 30);
        Color bg = Color.FromArgb(255, 40, 50, 60);

        string result = "hi".ToColour(fg, bg);

        string expected =
            ConsolePalette.BackgroundSgr(bg) +
            ConsolePalette.ForegroundSgr(fg) +
            "hi" +
            AnsiConsoleStringExtensions.Reset;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToColour_Uses_Default_Background_For_Transparent()
    {
        string result = "hi".ToColour(ConsolePalette.White, ConsolePalette.Transparent);

        Assert.Contains(Esc + "[49m", result);
    }

    [Fact]
    public void ToColour_Returns_Input_Unchanged_When_Empty()
    {
        Assert.Equal(string.Empty, string.Empty.ToColour(ConsolePalette.White, ConsolePalette.Black));
    }
}
