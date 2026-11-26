using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System.Configuration;

namespace Task.Monitor.Tests.Extensions;

public sealed class TextMetreExtensionsTests
{
    private static Theme StatusTheme()
    {
        Theme theme = new(new ConfigSection("theme-test"));

        theme.StatusOk      = ConsolePalette.Green;
        theme.StatusWarning = ConsolePalette.Yellow;
        theme.StatusError   = ConsolePalette.Red;

        return theme;
    }

    [Theory]
    [InlineData(0.0,  "ok")]
    [InlineData(0.49, "ok")]
    [InlineData(0.5,  "warning")]
    [InlineData(0.84, "warning")]
    [InlineData(0.85, "error")]
    [InlineData(1.0,  "error")]
    public void ToTextMetreColour_Follows_The_Load_Thresholds(double ratio, string status)
    {
        Theme theme = StatusTheme();

        Color expected = status switch {
            "ok"      => theme.StatusOk,
            "warning" => theme.StatusWarning,
                    _ => theme.StatusError
        };

        Assert.Equal(expected, ratio.ToTextMetreColour(theme));
    }

    // Every bar is ten cells wide whatever the load, so the column lines up. Any load shows a cell,
    // 0.3 doesn't round up to four cells, and values outside 0-1 are held to the ends.
    [Theory]
    [InlineData(0.0,        0)]
    [InlineData(0.001,      1)]
    [InlineData(0.04,       1)]
    [InlineData(0.25,       3)]
    [InlineData(0.3,        3)]
    [InlineData(0.5,        5)]
    [InlineData(0.7,        7)]
    [InlineData(1.0,        10)]
    [InlineData(1.2,        10)]
    [InlineData(-0.5,       0)]
    [InlineData(double.NaN, 0)]
    public void ToTextMetre_Fills_A_Cell_For_Each_Tenth(double ratio, int cells)
    {
        string metre = ratio.ToTextMetre();

        Assert.Equal(TextMetreExtensions.TextMetreCells, metre.Length);
        Assert.Equal(new string('⣿', cells).PadRight(TextMetreExtensions.TextMetreCells), metre);
    }
}
