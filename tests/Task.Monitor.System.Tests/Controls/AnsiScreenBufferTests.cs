using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls;

namespace Task.Monitor.System.Tests.Controls;

public sealed class AnsiScreenBufferTests
{
    private const char Esc = (char)27;

    private static string Fg(ConsoleColor c) => AnsiConsoleStringExtensions.GetForegroundCode(c).ToString();
    private static string Bg(ConsoleColor c) => AnsiConsoleStringExtensions.GetBackgroundCode(c).ToString();

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
        buffer.SetColour(ConsoleColor.Green, ConsoleColor.Black);

        // Background precedes foreground, matching AnsiConsoleStringExtensions.
        Assert.Equal(Bg(ConsoleColor.Black) + Fg(ConsoleColor.Green), buffer.AsSpan().ToString());
    }

    [Fact]
    public void SetColour_Is_Skipped_When_Colour_Is_Unchanged()
    {
        AnsiScreenBuffer buffer = new();
        buffer.SetColour(ConsoleColor.Green, ConsoleColor.Black);
        int afterFirst = buffer.Length;

        buffer.SetColour(ConsoleColor.Green, ConsoleColor.Black);

        Assert.Equal(afterFirst, buffer.Length);
    }

    [Fact]
    public void SetColour_Re_Emits_When_Colour_Changes()
    {
        AnsiScreenBuffer buffer = new();
        buffer.SetColour(ConsoleColor.Green, ConsoleColor.Black);
        buffer.SetColour(ConsoleColor.Red, ConsoleColor.Black);

        string expected = Bg(ConsoleColor.Black) + Fg(ConsoleColor.Green)
                        + Bg(ConsoleColor.Black) + Fg(ConsoleColor.Red);

        Assert.Equal(expected, buffer.AsSpan().ToString());
    }

    [Fact]
    public void Clear_Forces_Next_SetColour_To_Emit()
    {
        AnsiScreenBuffer buffer = new();
        buffer.SetColour(ConsoleColor.Green, ConsoleColor.Black);
        buffer.Clear();
        buffer.SetColour(ConsoleColor.Green, ConsoleColor.Black);

        Assert.Equal(Bg(ConsoleColor.Black) + Fg(ConsoleColor.Green), buffer.AsSpan().ToString());
    }

    [Fact]
    public void ResetColour_Emits_Reset_And_Forces_Next_SetColour_To_Emit()
    {
        AnsiScreenBuffer buffer = new();
        buffer.SetColour(ConsoleColor.Green, ConsoleColor.Black);
        buffer.ResetColour();
        buffer.SetColour(ConsoleColor.Green, ConsoleColor.Black);

        string colour = Bg(ConsoleColor.Black) + Fg(ConsoleColor.Green);
        Assert.Equal(colour + AnsiConsoleStringExtensions.Reset + colour, buffer.AsSpan().ToString());
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
