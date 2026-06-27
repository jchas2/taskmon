using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls;

namespace Task.Monitor.System.Tests.Controls;

public sealed class AnsiScreenBufferTests
{
    private const char Esc = (char)27;

    private static string Fg(Color c) => ConsolePalette.ForegroundSgr(c);
    private static string Bg(Color c) => ConsolePalette.BackgroundSgr(c);

    [Fact]
    public void Append_Char_And_Span_Accumulate_In_Order()
    {
        AnsiScreenBuffer buffer = new();
        buffer.Append('a');
        buffer.Append("bc");
        buffer.Append('d');

        Assert.Equal("abcd", buffer.AsSpan().ToString());
        Assert.Equal(4, buffer.Length);
    }

    [Fact]
    public void Append_Repeated_Char_Fills_Count()
    {
        AnsiScreenBuffer buffer = new();
        buffer.Append('-', 3);

        Assert.Equal("---", buffer.AsSpan().ToString());
    }

    [Fact]
    public void Clear_Resets_Length_For_Reuse()
    {
        AnsiScreenBuffer buffer = new();
        buffer.Append("first frame");
        buffer.Clear();
        buffer.Append("next");

        Assert.Equal("next", buffer.AsSpan().ToString());
    }

    [Theory]
    [InlineData(0, 0, "[1;1H")]
    [InlineData(4, 2, "[3;5H")]
    [InlineData(11, 9, "[10;12H")]
    public void MoveTo_Emits_OneBased_Row_Then_Column(int left, int top, string expectedTail)
    {
        AnsiScreenBuffer buffer = new();
        buffer.MoveTo(left, top);

        Assert.Equal(Esc + expectedTail, buffer.AsSpan().ToString());
    }

    [Fact]
    public void MoveTo_Floors_Negative_Coordinates()
    {
        AnsiScreenBuffer buffer = new();
        buffer.MoveTo(-5, -3);

        Assert.Equal(Esc + "[1;1H", buffer.AsSpan().ToString());
    }

    [Fact]
    public void SetColour_Emits_Background_Then_Foreground()
    {
        AnsiScreenBuffer buffer = new();
        buffer.SetColour(ConsolePalette.Green, ConsolePalette.Black);

        // Background precedes foreground, matching AnsiConsoleStringExtensions.
        Assert.Equal(Bg(ConsolePalette.Black) + Fg(ConsolePalette.Green), buffer.AsSpan().ToString());
    }

    [Fact]
    public void SetColour_Is_Skipped_When_Colour_Is_Unchanged()
    {
        AnsiScreenBuffer buffer = new();
        buffer.SetColour(ConsolePalette.Green, ConsolePalette.Black);
        int afterFirst = buffer.Length;

        buffer.SetColour(ConsolePalette.Green, ConsolePalette.Black);

        Assert.Equal(afterFirst, buffer.Length);
    }

    [Fact]
    public void SetColour_Re_Emits_When_Colour_Changes()
    {
        AnsiScreenBuffer buffer = new();
        buffer.SetColour(ConsolePalette.Green, ConsolePalette.Black);
        buffer.SetColour(ConsolePalette.Red, ConsolePalette.Black);

        string expected = Bg(ConsolePalette.Black) + Fg(ConsolePalette.Green)
                        + Bg(ConsolePalette.Black) + Fg(ConsolePalette.Red);

        Assert.Equal(expected, buffer.AsSpan().ToString());
    }

    [Fact]
    public void Clear_Forces_Next_SetColour_To_Emit()
    {
        AnsiScreenBuffer buffer = new();
        buffer.SetColour(ConsolePalette.Green, ConsolePalette.Black);
        buffer.Clear();
        buffer.SetColour(ConsolePalette.Green, ConsolePalette.Black);

        Assert.Equal(Bg(ConsolePalette.Black) + Fg(ConsolePalette.Green), buffer.AsSpan().ToString());
    }

    [Fact]
    public void ResetColour_Emits_Reset_And_Forces_Next_SetColour_To_Emit()
    {
        AnsiScreenBuffer buffer = new();
        buffer.SetColour(ConsolePalette.Green, ConsolePalette.Black);
        buffer.ResetColour();
        buffer.SetColour(ConsolePalette.Green, ConsolePalette.Black);

        string colour = Bg(ConsolePalette.Black) + Fg(ConsolePalette.Green);
        Assert.Equal(colour + AnsiConsoleStringExtensions.Reset + colour, buffer.AsSpan().ToString());
    }

    [Fact]
    public void SetBold_Emits_Bold_On_Then_Off()
    {
        AnsiScreenBuffer buffer = new();
        buffer.SetBold(true);
        buffer.Append('x');
        buffer.SetBold(false);

        Assert.Equal(Esc + "[1m" + "x" + Esc + "[22m", buffer.AsSpan().ToString());
    }

    [Fact]
    public void SetBold_Is_Skipped_When_State_Is_Unchanged()
    {
        AnsiScreenBuffer buffer = new();
        buffer.SetBold(true);
        int afterFirst = buffer.Length;

        buffer.SetBold(true);

        Assert.Equal(afterFirst, buffer.Length);
    }

    [Fact]
    public void Clear_Forces_Next_SetBold_To_Emit()
    {
        AnsiScreenBuffer buffer = new();
        buffer.SetBold(true);
        buffer.Clear();
        buffer.SetBold(true);

        Assert.Equal(Esc + "[1m", buffer.AsSpan().ToString());
    }

    [Fact]
    public void ResetColour_Clears_Bold_State()
    {
        AnsiScreenBuffer buffer = new();
        buffer.SetBold(true);
        buffer.ResetColour();
        buffer.SetBold(true);

        Assert.Equal(Esc + "[1m" + AnsiConsoleStringExtensions.Reset + Esc + "[1m", buffer.AsSpan().ToString());
    }

    [Fact]
    public void Growth_Preserves_Content_Beyond_Initial_Capacity()
    {
        AnsiScreenBuffer buffer = new(capacity: 4);
        string payload = new('x', 1000);
        buffer.Append(payload);

        Assert.Equal(payload, buffer.AsSpan().ToString());
        Assert.Equal(1000, buffer.Length);
    }
}
