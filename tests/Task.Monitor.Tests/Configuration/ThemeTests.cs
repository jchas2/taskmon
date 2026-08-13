using System.Drawing;
using System.Text.RegularExpressions;
using Task.Monitor.Configuration;
using Task.Monitor.System.Configuration;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Configuration;

public sealed class ThemeTests
{
    private static string ThemeIni => @"
[Test Theme]
colour-mode=truecolour

background=#0f1610
background-highlight=#1d4125

col-cmd-normal-user-space=#327f77
col-cmd-low-priority=#10b981
col-cmd-high-cpu=#b082d1
col-cmd-io-bound=#dff0e6
col-cmd-script=#dff0e6
col-user-current-non-root=#b082d1
col-user-other-non-root=#b082d1
col-user-system=#73fa91
col-user-root=#73fa91

command-foreground=#717f24
command-background=#121d18

delta-highlight-colour=#bda25c

error=#a6423f

foreground=#717f24
foreground-highlight=#73fa91

menubar-foreground=#717f24
menubar-background=#121d18

range-high-background=#b082d1
range-low-background=#10b981
range-mid-background=#ffd085
range-high-foreground=#000000
range-low-foreground=#000000
range-mid-foreground=#000000

header-background=#121d18
header-foreground=#717f24
";    
    
    // A persisted colour value must be hex (#RRGGBB / #AARRGGBB) or the transparent token.
    private static readonly Regex HexOrTransparent =
        new("^(#[0-9A-Fa-f]{6}|#[0-9A-Fa-f]{8}|transparent)$", RegexOptions.Compiled);

    [Fact]
    public void Theme_Canary_Test() =>
        Assert.Equal(29, CanaryTestHelper.GetPropertyCount<Theme>());

    [Fact]
    public void Constructor_Initialises_Successfully()
    {
        ConfigParser parser = new(ThemeIni);
        parser.Parse();
        Theme theme = new(parser.Sections[0]);
        
        AssertThemeColours(theme);
    }

    [Fact]
    public void Setters_Initialise_Successfully()
    {
        ConfigSection section = new("Test Theme");
        
        Theme theme = new(section) {
            Background                   = ColorTranslator.FromHtml("#0f1610"),
            BackgroundHighlight          = ColorTranslator.FromHtml("#1d4125"),
            ColumnCommandNormalUserSpace = ColorTranslator.FromHtml("#327f77"),
            ColumnCommandLowPriority     = ColorTranslator.FromHtml("#10b981"),
            ColumnCommandHighCpu         = ColorTranslator.FromHtml("#b082d1"),
            ColumnCommandIoBound         = ColorTranslator.FromHtml("#dff0e6"),
            ColumnCommandScript          = ColorTranslator.FromHtml("#dff0e6"),
            ColumnUserCurrentNonRoot     = ColorTranslator.FromHtml("#b082d1"),
            ColumnUserOtherNonRoot       = ColorTranslator.FromHtml("#b082d1"),
            ColumnUserSystem             = ColorTranslator.FromHtml("#73fa91"),
            ColumnUserRoot               = ColorTranslator.FromHtml("#73fa91"),
            CommandBackground            = ColorTranslator.FromHtml("#121d18"),
            CommandForeground            = ColorTranslator.FromHtml("#717f24"),
            DeltaHighlightColour         = ColorTranslator.FromHtml("#bda25c"),
            Error                        = ColorTranslator.FromHtml("#a6423f"),
            Foreground                   = ColorTranslator.FromHtml("#717f24"),
            ForegroundHighlight          = ColorTranslator.FromHtml("#73fa91"),
            HeaderBackground             = ColorTranslator.FromHtml("#121d18"),
            HeaderForeground             = ColorTranslator.FromHtml("#717f24"),
            MenubarBackground            = ColorTranslator.FromHtml("#121d18"),
            MenubarForeground            = ColorTranslator.FromHtml("#717f24"),
            RangeHighBackground          = ColorTranslator.FromHtml("#b082d1"),
            RangeLowBackground           = ColorTranslator.FromHtml("#10b981"),
            RangeMidBackground           = ColorTranslator.FromHtml("#ffd085"),
            RangeHighForeground          = ColorTranslator.FromHtml("#000000"),
            RangeLowForeground           = ColorTranslator.FromHtml("#000000"),
            RangeMidForeground           = ColorTranslator.FromHtml("#000000")
        };
        
        AssertThemeColours(theme);
    }
    
    private void AssertThemeColours(Theme theme)
    {
        Assert.Equal(ColorTranslator.FromHtml("#0f1610"), theme.Background);
        Assert.Equal(ColorTranslator.FromHtml("#1d4125"), theme.BackgroundHighlight);
        Assert.Equal(ColorTranslator.FromHtml("#327f77"), theme.ColumnCommandNormalUserSpace);
        Assert.Equal(ColorTranslator.FromHtml("#10b981"), theme.ColumnCommandLowPriority);
        Assert.Equal(ColorTranslator.FromHtml("#b082d1"), theme.ColumnCommandHighCpu);
        Assert.Equal(ColorTranslator.FromHtml("#dff0e6"), theme.ColumnCommandIoBound);
        Assert.Equal(ColorTranslator.FromHtml("#dff0e6"), theme.ColumnCommandScript);
        Assert.Equal(ColorTranslator.FromHtml("#b082d1"), theme.ColumnUserCurrentNonRoot);
        Assert.Equal(ColorTranslator.FromHtml("#b082d1"), theme.ColumnUserOtherNonRoot);
        Assert.Equal(ColorTranslator.FromHtml("#73fa91"), theme.ColumnUserSystem);
        Assert.Equal(ColorTranslator.FromHtml("#73fa91"), theme.ColumnUserRoot);
        Assert.Equal(ColorTranslator.FromHtml("#121d18"), theme.CommandBackground);
        Assert.Equal(ColorTranslator.FromHtml("#717f24"), theme.CommandForeground);
        Assert.Equal(ColorTranslator.FromHtml("#bda25c"), theme.DeltaHighlightColour);
        Assert.Equal(ColorTranslator.FromHtml("#a6423f"), theme.Error);
        Assert.Equal(ColorTranslator.FromHtml("#717f24"), theme.Foreground);
        Assert.Equal(ColorTranslator.FromHtml("#73fa91"), theme.ForegroundHighlight);
        Assert.Equal(ColorTranslator.FromHtml("#121d18"), theme.HeaderBackground);
        Assert.Equal(ColorTranslator.FromHtml("#717f24"), theme.HeaderForeground);
        Assert.Equal(ColorTranslator.FromHtml("#121d18"), theme.MenubarBackground);
        Assert.Equal(ColorTranslator.FromHtml("#717f24"), theme.MenubarForeground);
        Assert.Equal(ColorTranslator.FromHtml("#b082d1"), theme.RangeHighBackground);
        Assert.Equal(ColorTranslator.FromHtml("#10b981"), theme.RangeLowBackground);
        Assert.Equal(ColorTranslator.FromHtml("#ffd085"), theme.RangeMidBackground);
        Assert.Equal(ColorTranslator.FromHtml("#000000"), theme.RangeHighForeground);
        Assert.Equal(ColorTranslator.FromHtml("#000000"), theme.RangeLowForeground);
        Assert.Equal(ColorTranslator.FromHtml("#000000"), theme.RangeMidForeground);        
    }
    
    [Fact]
    public void Normalize_Converts_Legacy_Names_To_Hex()
    {
        ConfigSection section = new("theme-test");
        section.Add(Constants.Keys.Background, "black");
        section.Add(Constants.Keys.Foreground, "White");
        section.Add(Constants.Keys.MenubarBackground, "DarkBlue");

        new Theme(section).Normalize();

        Assert.Equal("#000000", section.GetString(Constants.Keys.Background));
        Assert.Equal("#FFFFFF", section.GetString(Constants.Keys.Foreground));
        Assert.Equal("#000080", section.GetString(Constants.Keys.MenubarBackground));
    }

    [Fact]
    public void Normalize_Preserves_Transparent_Token()
    {
        ConfigSection section = new("theme-test");
        section.Add(Constants.Keys.Background, "transparent");

        new Theme(section).Normalize();

        Assert.Equal("transparent", section.GetString(Constants.Keys.Background));
    }

    [Fact]
    public void Normalize_Is_Idempotent_For_Hex_Values()
    {
        ConfigSection section = new("theme-test");
        section.Add(Constants.Keys.Background, "#123456");

        new Theme(section).Normalize();

        Assert.Equal("#123456", section.GetString(Constants.Keys.Background));
    }

    [Fact]
    public void Normalize_Does_Not_Add_Missing_Keys()
    {
        ConfigSection section = new("theme-test");
        section.Add(Constants.Keys.Background, "black");

        new Theme(section).Normalize();

        Assert.False(section.Contains(Constants.Keys.Foreground));
    }
}
