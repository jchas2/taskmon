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

    // TODO:
    private static readonly string[] PredefinedThemes =
    [
        Constants.Sections.ThemeTaskmonDefault,
        Constants.Sections.ThemeMsDos,
        // Constants.Sections.ThemeMono,
        // Constants.Sections.ThemeTokyoNight,
        // Constants.Sections.ThemeMatrix,
        // Constants.Sections.ThemeSolar,
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
}
