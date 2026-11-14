using System.Globalization;
using Task.Monitor.Resources;

namespace Task.Monitor.Tests.Resources;

public sealed class UiLanguageTests
{
    [Theory]
    [InlineData("en-US", "en-AU", "en-AU")]
    [InlineData("en-US", "en-GB", "en-GB")]
    [InlineData("en-GB", "en-US", "en-US")]
    public void An_English_Display_Language_Takes_The_Spelling_Of_An_English_Region(
        string display,
        string region,
        string expected) =>
        Assert.Equal(expected, Resolve(null, null, display, region)?.Name);

    [Fact]
    public void An_Invariant_Display_Language_Is_Kept() =>
        Assert.Null(UiLanguage.Resolve(Env(null), null, CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("en-AU")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a culture")]
    public void A_Missing_Or_Unknown_Config_Language_Is_Ignored(string? configured) =>
        Assert.Equal("en-AU", Resolve(null, configured, "en-US", "en-AU")?.Name);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a culture")]
    public void A_Missing_Or_Unknown_Environment_Variable_Is_Ignored(string? uiLanguage) =>
        Assert.Null(Resolve(uiLanguage, null, "de-DE", "de-DE"));

    private static Func<string, string?> Env(string? uiLanguage) =>
        name => name == UiLanguage.UiLanguageEnvVar ? uiLanguage : null;

    private static CultureInfo? Resolve(string? uiLanguage, string? configured, string display, string region) =>
        UiLanguage.Resolve(Env(uiLanguage), configured, CultureInfo.GetCultureInfo(display), CultureInfo.GetCultureInfo(region));

    // ui-language in taskmon.ini wins over the display language and region.
    [Theory]
    [InlineData("it-IT",   "en-US", "en-AU")]
    [InlineData(" it ",    "en-GB", "en-GB")]
    [InlineData("en-US",   "en-AU", "en-AU")]
    [InlineData("en-GB",   "de-DE", "de-DE")]
    public void The_Config_Language_Wins_Over_The_Display_Language_And_Region(
        string configured,
        string display,
        string region) =>
        Assert.Equal(configured.Trim(), Resolve(null, configured, display, region)?.Name);

    [Theory]
    [InlineData("en-US", "en-US")]
    [InlineData("en-AU", "en-AU")]
    [InlineData("en-US", "de-DE")]
    [InlineData("de-DE", "en-AU")]
    [InlineData("fr-FR", "fr-FR")]
    public void The_Display_Language_Is_Kept_Otherwise(string display, string region) =>
        Assert.Null(Resolve(null, null, display, region));

    // TASKMON_UI_LANGUAGE is a one-off override, so it wins over the config file too.
    [Theory]
    [InlineData("en-US", null,    "en-GB", "en-AU")]
    [InlineData("en-GB", null,    "en-US", "en-US")]
    [InlineData("de-DE", null,    "en-US", "en-US")]
    [InlineData(" fr-FR ", null,  "en-AU", "en-AU")]
    [InlineData("en-GB", "it-IT", "en-US", "en-AU")]
    public void The_Environment_Variable_Wins_Over_The_Config_File_Display_Language_And_Region(
        string  uiLanguage,
        string? configured,
        string  display,
        string  region) =>
        Assert.Equal(uiLanguage.Trim(), Resolve(uiLanguage, configured, display, region)?.Name);
}
