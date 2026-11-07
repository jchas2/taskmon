using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Tests.Controls;

namespace Task.Monitor.Tests.Gui.Controls.Performance;

public sealed class PerformancePanelTitleTests
{
    private static readonly Color StripColour = ColorTranslator.FromHtml("#1a1b26");
    private static readonly Color TextColour  = ColorTranslator.FromHtml("#9ece6a");
    private static readonly Color PanelColour = ColorTranslator.FromHtml("#0f1610");

    private const int PanelWidth = 42;
    private const int TextWidth  = PanelWidth - 14;
    private const int TitleRow   = 1;

    // Regression test: on themes with a transparent panel background (e.g. Tokyo Night) the
    // half-cell edges of an unselected panel's title strip drew a bar in the title text colour.
    // Each edge cell must be half panel background (outside) and half strip colour (inside).
    [Theory]
    [InlineData(true,  false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true,  true)]
    public void Unselected_Title_Edges_Are_Half_Panel_Background_And_Half_Strip(
        bool panelTransparent,
        bool stripTransparent)
    {
        Color panelBg = panelTransparent ? ConsolePalette.Transparent : PanelColour;
        Color stripBg = stripTransparent ? ConsolePalette.Transparent : StripColour;

        AnsiScreen screen = new(DrawUnselectedPanel(panelBg, stripBg));

        string outside = AnsiScreen.Key(panelBg);
        string inside  = AnsiScreen.Key(stripBg);

        Assert.Equal((outside, inside), screen.HalvesAt(x: 0,             y: TitleRow));
        Assert.Equal((inside, outside), screen.HalvesAt(x: TextWidth - 1, y: TitleRow));
    }

    private static string DrawUnselectedPanel(Color panelBg, Color stripBg)
    {
        RecordingTerminal terminal = new();
        AppConfig appConfig        = new RunContextHelper().GetRunContext().AppConfig;

        appConfig.Theme.MenubarBackground = stripBg;
        appConfig.Theme.MenubarForeground = TextColour;

        PerformancePanelControl panel = new(
            terminal,
            new Chart(terminal),
            new Control(terminal),
            key:   "cpu",
            title: "CPU",
            appConfig) {
            X                = 0,
            Y                = 0,
            Width            = PanelWidth,
            Height           = 7,
            BackgroundColour = panelBg,
            ForegroundColour = TextColour,
            IsSelected       = false
        };

        panel.Resize();
        terminal.Reset();
        panel.Draw();

        return terminal.Output;
    }
}
