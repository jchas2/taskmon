using Task.Monitor.Cli.Utils;
using ChartControl = Task.Monitor.System.Controls.Chart.Chart;

namespace Task.Monitor.System.Tests.Controls.Chart;

public sealed class ChartTests
{
    private const char Esc = (char)27;

    private static ChartControl CreateChart(RecordingTerminal terminal, int width, int height)
    {
        ChartControl chart = new(terminal)
        {
            X = 0,
            Y = 0,
            Width = width,
            Height = height
        };

        chart.Resize();
        return chart;
    }

    [Fact]
    public void OnDraw_Blits_The_Frame_With_A_Single_Span_Write()
    {
        RecordingTerminal terminal = new();
        ChartControl chart = CreateChart(terminal, width: 10, height: 6);

        for (int i = 0; i < 8; i++) {
            chart.Add(0.5);
        }

        terminal.Reset();
        chart.Draw();

        Assert.Equal(1, terminal.WriteSpanCalls);
        Assert.Equal(0, terminal.WriteCharCalls);
        Assert.Equal(0, terminal.WriteStringCalls);
    }

    [Fact]
    public void OnDraw_Does_Not_Use_The_Console_Colour_Or_Cursor_Apis()
    {
        RecordingTerminal terminal = new();
        ChartControl chart = CreateChart(terminal, width: 10, height: 6);

        for (int i = 0; i < 8; i++) {
            chart.Add(0.5);
        }

        terminal.Reset();
        chart.Draw();

        Assert.Equal(0, terminal.SetCursorPositionCalls);
        Assert.Equal(0, terminal.ForegroundColorSets);
        Assert.Equal(0, terminal.BackgroundColorSets);
    }

    [Fact]
    public void OnDraw_Emits_Border_Cursor_Moves_Colour_And_Reset()
    {
        RecordingTerminal terminal = new();
        ChartControl chart = CreateChart(terminal, width: 10, height: 6);
        chart.Add(0.5);

        terminal.Reset();
        chart.Draw();

        string output = terminal.Output;

        Assert.Contains(Esc + "[1;1H", output);

        Assert.Contains('╭', output);
        Assert.Contains('╮', output);
        Assert.Contains('╰', output);
        Assert.Contains('╯', output);

        Assert.Contains(AnsiConsoleStringExtensions.GetForegroundCode(chart.ForegroundColour).ToString(), output);
        Assert.Contains(AnsiConsoleStringExtensions.GetBackgroundCode(chart.BackgroundColour).ToString(), output);

        Assert.EndsWith(AnsiConsoleStringExtensions.Reset, output);
    }

    [Fact]
    public void OnDraw_Emits_The_Low_Bar_Colour_For_Small_Values()
    {
        RecordingTerminal terminal = new();
        ChartControl chart = CreateChart(terminal, width: 10, height: 6);
        
        chart.AutoScale = false;
        chart.Add(0.1);

        terminal.Reset();
        chart.Draw();

        Assert.Contains(
            AnsiConsoleStringExtensions.GetForegroundCode(chart.ColourLow).ToString(),
            terminal.Output);
    }
}
