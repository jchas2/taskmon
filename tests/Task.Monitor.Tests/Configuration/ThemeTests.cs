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

chart-border=#334455
chart-y-axis=#667788

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
        Assert.Equal(31, CanaryTestHelper.GetPropertyCount<Theme>());

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
            ChartBprder                  = ColorTranslator.FromHtml("#334455"),
            ChartYAxis                   = ColorTranslator.FromHtml("#667788"),
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

    [Fact]
    public void Setters_ReadBack_All_Colour_Properties_Successfully()
    {
        // Arrange – use a distinct colour per property so a mis-mapped setter is caught.
        ConfigSection section = new("Read-Back Theme");

        Theme theme = new(section);

        // Act – write every colour property through its setter.
        theme.Background                  = ColorTranslator.FromHtml("#010101");
        theme.BackgroundHighlight         = ColorTranslator.FromHtml("#020202");
        theme.ChartBprder                 = ColorTranslator.FromHtml("#1c1c1c");
        theme.ChartYAxis                  = ColorTranslator.FromHtml("#1d1d1d");
        theme.ColumnCommandNormalUserSpace = ColorTranslator.FromHtml("#030303");
        theme.ColumnCommandLowPriority    = ColorTranslator.FromHtml("#040404");
        theme.ColumnCommandHighCpu        = ColorTranslator.FromHtml("#050505");
        theme.ColumnCommandIoBound        = ColorTranslator.FromHtml("#060606");
        theme.ColumnCommandScript         = ColorTranslator.FromHtml("#070707");
        theme.ColumnUserCurrentNonRoot    = ColorTranslator.FromHtml("#080808");
        theme.ColumnUserOtherNonRoot      = ColorTranslator.FromHtml("#090909");
        theme.ColumnUserSystem            = ColorTranslator.FromHtml("#0a0a0a");
        theme.ColumnUserRoot              = ColorTranslator.FromHtml("#0b0b0b");
        theme.CommandBackground           = ColorTranslator.FromHtml("#0c0c0c");
        theme.CommandForeground           = ColorTranslator.FromHtml("#0d0d0d");
        theme.DeltaHighlightColour        = ColorTranslator.FromHtml("#0e0e0e");
        theme.Error                       = ColorTranslator.FromHtml("#0f0f0f");
        theme.Foreground                  = ColorTranslator.FromHtml("#101010");
        theme.ForegroundHighlight         = ColorTranslator.FromHtml("#111111");
        theme.HeaderBackground            = ColorTranslator.FromHtml("#121212");
        theme.HeaderForeground            = ColorTranslator.FromHtml("#131313");
        theme.MenubarBackground           = ColorTranslator.FromHtml("#141414");
        theme.MenubarForeground           = ColorTranslator.FromHtml("#151515");
        theme.RangeHighBackground         = ColorTranslator.FromHtml("#161616");
        theme.RangeLowBackground          = ColorTranslator.FromHtml("#171717");
        theme.RangeMidBackground          = ColorTranslator.FromHtml("#181818");
        theme.RangeHighForeground         = ColorTranslator.FromHtml("#191919");
        theme.RangeLowForeground          = ColorTranslator.FromHtml("#1a1a1a");
        theme.RangeMidForeground          = ColorTranslator.FromHtml("#1b1b1b");

        // Assert – read every colour property back through its getter.
        Assert.Equal(ColorTranslator.FromHtml("#010101"), theme.Background);
        Assert.Equal(ColorTranslator.FromHtml("#020202"), theme.BackgroundHighlight);
        Assert.Equal(ColorTranslator.FromHtml("#1c1c1c"), theme.ChartBprder);
        Assert.Equal(ColorTranslator.FromHtml("#1d1d1d"), theme.ChartYAxis);
        Assert.Equal(ColorTranslator.FromHtml("#030303"), theme.ColumnCommandNormalUserSpace);
        Assert.Equal(ColorTranslator.FromHtml("#040404"), theme.ColumnCommandLowPriority);
        Assert.Equal(ColorTranslator.FromHtml("#050505"), theme.ColumnCommandHighCpu);
        Assert.Equal(ColorTranslator.FromHtml("#060606"), theme.ColumnCommandIoBound);
        Assert.Equal(ColorTranslator.FromHtml("#070707"), theme.ColumnCommandScript);
        Assert.Equal(ColorTranslator.FromHtml("#080808"), theme.ColumnUserCurrentNonRoot);
        Assert.Equal(ColorTranslator.FromHtml("#090909"), theme.ColumnUserOtherNonRoot);
        Assert.Equal(ColorTranslator.FromHtml("#0a0a0a"), theme.ColumnUserSystem);
        Assert.Equal(ColorTranslator.FromHtml("#0b0b0b"), theme.ColumnUserRoot);
        Assert.Equal(ColorTranslator.FromHtml("#0c0c0c"), theme.CommandBackground);
        Assert.Equal(ColorTranslator.FromHtml("#0d0d0d"), theme.CommandForeground);
        Assert.Equal(ColorTranslator.FromHtml("#0e0e0e"), theme.DeltaHighlightColour);
        Assert.Equal(ColorTranslator.FromHtml("#0f0f0f"), theme.Error);
        Assert.Equal(ColorTranslator.FromHtml("#101010"), theme.Foreground);
        Assert.Equal(ColorTranslator.FromHtml("#111111"), theme.ForegroundHighlight);
        Assert.Equal(ColorTranslator.FromHtml("#121212"), theme.HeaderBackground);
        Assert.Equal(ColorTranslator.FromHtml("#131313"), theme.HeaderForeground);
        Assert.Equal(ColorTranslator.FromHtml("#141414"), theme.MenubarBackground);
        Assert.Equal(ColorTranslator.FromHtml("#151515"), theme.MenubarForeground);
        Assert.Equal(ColorTranslator.FromHtml("#161616"), theme.RangeHighBackground);
        Assert.Equal(ColorTranslator.FromHtml("#171717"), theme.RangeLowBackground);
        Assert.Equal(ColorTranslator.FromHtml("#181818"), theme.RangeMidBackground);
        Assert.Equal(ColorTranslator.FromHtml("#191919"), theme.RangeHighForeground);
        Assert.Equal(ColorTranslator.FromHtml("#1a1a1a"), theme.RangeLowForeground);
        Assert.Equal(ColorTranslator.FromHtml("#1b1b1b"), theme.RangeMidForeground);
    }

    private void AssertThemeColours(Theme theme)
    {
        Assert.Equal(ColorTranslator.FromHtml("#0f1610"), theme.Background);
        Assert.Equal(ColorTranslator.FromHtml("#1d4125"), theme.BackgroundHighlight);
        Assert.Equal(ColorTranslator.FromHtml("#334455"), theme.ChartBprder);
        Assert.Equal(ColorTranslator.FromHtml("#667788"), theme.ChartYAxis);
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
