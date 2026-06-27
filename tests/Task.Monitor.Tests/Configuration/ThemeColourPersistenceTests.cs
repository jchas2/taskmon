using System.Text.RegularExpressions;
using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Configuration;

namespace Task.Monitor.Tests.Configuration;

public sealed class ThemeColourPersistenceTests
{
    // A persisted colour value must be hex (#RRGGBB / #AARRGGBB) or the transparent token.
    private static readonly Regex HexOrTransparent =
        new("^(#[0-9A-Fa-f]{6}|#[0-9A-Fa-f]{8}|transparent)$", RegexOptions.Compiled);

    private static readonly string[] PredefinedThemes =
    [
        Constants.Sections.ThemeColour,
        Constants.Sections.ThemeMono,
        Constants.Sections.ThemeMsDos,
        Constants.Sections.ThemeTokyoNight,
        Constants.Sections.ThemeMatrix,
        Constants.Sections.ThemeSolar,
    ];

    private static readonly string[] ColourKeys =
    [
        Constants.Keys.Background, Constants.Keys.BackgroundHighlight,
        Constants.Keys.ColCmdNormalUserSpace, Constants.Keys.ColCmdLowPriority,
        Constants.Keys.ColCmdHighCpu, Constants.Keys.ColCmdIoBound, Constants.Keys.ColCmdScript,
        Constants.Keys.ColUserCurrentNonRoot, Constants.Keys.ColUserOtherNonRoot,
        Constants.Keys.ColUserSystem, Constants.Keys.ColUserRoot,
        Constants.Keys.CommandBackground, Constants.Keys.CommandForeground,
        Constants.Keys.Error, Constants.Keys.Foreground, Constants.Keys.ForegroundHighlight,
        Constants.Keys.HeaderBackground, Constants.Keys.HeaderForeground,
        Constants.Keys.MenubarBackground, Constants.Keys.MenubarForeground,
        Constants.Keys.RangeHighBackground, Constants.Keys.RangeLowBackground,
        Constants.Keys.RangeMidBackground, Constants.Keys.RangeHighForeground,
        Constants.Keys.RangeLowForeground, Constants.Keys.RangeMidForeground,
    ];

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

    [Fact]
    public void Loading_Config_Rewrites_Named_Theme_Colours_As_Hex()
    {
        Config config = Config.FromString(
            "[theme-colour]\nbackground=black\nforeground=white\nmenubar-background=darkblue\n")!;

        _ = new AppConfig(new Mock<IFileSystem>().Object, config);

        ConfigSection section = config.GetConfigSection(Constants.Sections.ThemeColour);
        
        Assert.Equal("#000000", section.GetString(Constants.Keys.Background));
        Assert.Equal("#FFFFFF", section.GetString(Constants.Keys.Foreground));
        Assert.Equal("#000080", section.GetString(Constants.Keys.MenubarBackground));
    }

    [Fact]
    public void Seeded_Themes_Persist_Only_Hex_Or_Transparent_Colours()
    {
        Config config = new();

        _ = new AppConfig(new Mock<IFileSystem>().Object, config);

        foreach (string themeName in PredefinedThemes) {
            ConfigSection section = config.GetConfigSection(themeName);

            foreach (string key in ColourKeys) {
                if (!section.Contains(key)) {
                    continue;
                }

                string value = section.GetString(key);

                Assert.True(
                    HexOrTransparent.IsMatch(value),
                    $"{themeName}.{key} = '{value}' is not a hex colour or 'transparent'.");
            }
        }
    }
}
