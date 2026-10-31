using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Configuration;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class CommonControlTests
{
    private static Theme CreateTheme()
    {
        ConfigSection section = new("theme-test");
        section.Add(Constants.Keys.HotKey, "#112233");
        section.Add(Constants.Keys.HotKeyName, "#445566");
        section.Add(Constants.Keys.ListViewBorderBackground, "#778899");

        return new Theme(section);
    }

    [Fact]
    public void FooterText_Colours_Keys_And_Labels_From_The_Theme()
    {
        Theme theme = CreateTheme();

        string text = CommonControl.FooterText(theme, ("s", "Sort"), ("k", "End Task"));

        string expected =
            ConsolePalette.BackgroundSgr(ColorTranslator.FromHtml("#778899")) +
            ConsolePalette.ForegroundSgr(ColorTranslator.FromHtml("#112233")) + "s" +
            ConsolePalette.ForegroundSgr(ColorTranslator.FromHtml("#445566")) + " Sort" +
            CommonControl.FooterSeparator +
            ConsolePalette.ForegroundSgr(ColorTranslator.FromHtml("#112233")) + "k" +
            ConsolePalette.ForegroundSgr(ColorTranslator.FromHtml("#445566")) + " End Task";

        Assert.Equal(expected, text);
    }

    [Fact]
    public void FooterText_Is_Empty_Without_HotKeys() =>
        Assert.Equal(string.Empty, CommonControl.FooterText(CreateTheme()));

    [Fact]
    public void FooterText_Keeps_The_Given_Order_And_Formats_Every_Key()
    {
        string text = CommonControl.FooterText(
            CreateTheme(),
            CommonControl.SortHotKey,
            CommonControl.FilterHotKey,
            CommonControl.ClearFilterHotKey,
            CommonControl.ScrollHotKey,
            ("F1", "Help"));

        Assert.Equal(
            "s Sort   f Filter   esc Clear Filter   ↑ ↓ pgup pgdn Scroll   F1 Help",
            AnsiTestHelper.StripColours(text));
    }

    [Theory]
    [InlineData("s",   "s")]
    [InlineData("S",   "s")]
    [InlineData("Esc", "esc")]
    [InlineData("↑ ↓ PgUp PgDn", "↑ ↓ pgup pgdn")]
    [InlineData("F1",  "F1")]
    [InlineData("f10", "F10")]
    [InlineData("F24", "F24")]
    [InlineData("F25", "f25")]
    [InlineData("F",   "f")]
    public void FormatHotKey_Lower_Cases_All_But_Function_Keys(string key, string expected) =>
        Assert.Equal(expected, CommonControl.FormatHotKey(key));
}
