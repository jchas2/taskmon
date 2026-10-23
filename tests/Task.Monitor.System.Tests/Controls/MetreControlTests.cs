using System.Drawing;
using System.Text.RegularExpressions;
using Task.Monitor.System.Controls.Metre;

namespace Task.Monitor.System.Tests.Controls;

public sealed class MetreControlTests
{
    private static string[] DrawRows(MetreTextPosition position)
    {
        RecordingTerminal terminal = new();

        MetreControl metre = new(terminal) {
            Border       = true,
            ShowLegend   = false,
            Rows         = 1,
            Width        = 20,
            Text         = "CPU%",
            TextPosition = position
        };

        metre.AddSeries("Total", Color.Green);
        metre.Draw();

        string plain = Regex.Replace(terminal.Output, @"\x1b\[[0-9;]*m", string.Empty);

        return Regex.Split(plain, @"\x1b\[[0-9;]*H").Where(row => row.Length > 0).ToArray();
    }

    [Fact]
    public void Draws_The_Text_At_The_Left_Of_The_Top_Border()
    {
        string[] rows = DrawRows(MetreTextPosition.TopLeft);

        Assert.Equal("╭─ CPU% ───────────╮", rows[0]);
        Assert.Equal("╰──────────────────╯", rows[^1]);
    }

    [Fact]
    public void Draws_The_Text_Centred_In_The_Bottom_Border_By_Default()
    {
        string[] rows = DrawRows(MetreTextPosition.BottomCentre);

        Assert.Equal("╭──────────────────╮", rows[0]);
        Assert.Equal("╰────── CPU% ──────╯", rows[^1]);
    }

    [Fact]
    public void Draws_The_Text_Centred_In_The_Top_Border()
    {
        string[] rows = DrawRows(MetreTextPosition.TopCentre);

        Assert.Equal("╭────── CPU% ──────╮", rows[0]);
        Assert.Equal("╰──────────────────╯", rows[^1]);
    }
}
