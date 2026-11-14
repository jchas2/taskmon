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
    [InlineData("de",      "de")]
    [InlineData("de-AT",   "de")]
    [InlineData("it-IT",   "it")]
    [InlineData(" fr-CA ", "fr")]
    [InlineData("da-DK",   "da")]
    [InlineData("pt-BR",   "pt-BR")]
    [InlineData("en-GB",   "en-GB")]
    [InlineData("en-us",   "en-US")]
    public void A_Language_Finds_The_Choice_Of_Its_Translation(string configured, string expected) =>
        Assert.Equal(expected, UiLanguage.Find(configured)?.Code);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a culture")]
    [InlineData("en")]
    [InlineData("en-AU")]
    [InlineData("pt-PT")]
    [InlineData("sv-SE")]
    public void A_Language_Without_A_Translation_Of_Its_Own_Is_Not_A_Choice(string? configured) =>
        Assert.Null(UiLanguage.Find(configured));

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

    [Fact]
    public void Each_Choice_Is_Its_Own_Language_Listed_By_Name()
    {
        Assert.Equal(UiLanguage.Choices.Select(c => c.Code).Distinct(), UiLanguage.Choices.Select(c => c.Code));
        Assert.Equal(UiLanguage.Choices.OrderBy(c => c.NativeName, StringComparer.InvariantCulture), UiLanguage.Choices);
        Assert.All(UiLanguage.Choices, choice => Assert.Same(choice, UiLanguage.Find(choice.Code)));
    }

    private static Func<string, string?> Env(string? uiLanguage) =>
        name => name == UiLanguage.UiLanguageEnvVar ? uiLanguage : null;

    // Each translation ships as <code>/taskmon.resources.dll, so a language added without a Setup
    // row fails here.
    [Fact]
    public void Every_Translation_Is_A_Choice()
    {
        string satellite = $"{typeof(Strings).Assembly.GetName().Name}.resources.dll";

        List<string> translations = Directory.GetDirectories(AppContext.BaseDirectory)
            .Where(folder => File.Exists(Path.Combine(folder, satellite)))
            .Select(Path.GetFileName)
            .Select(code => code!)
            .ToList();

        Assert.NotEmpty(translations);
        Assert.All(translations, code => Assert.Equal(code, UiLanguage.Find(code)?.Code, StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(null,            false)]
    [InlineData("",              false)]
    [InlineData("not a culture", false)]
    [InlineData("de-DE",         true)]
    [InlineData("en-AU",         true)]
    public void Only_A_Valid_Environment_Variable_Overrides_The_Choice(string? uiLanguage, bool expected) =>
        Assert.Equal(expected, UiLanguage.IsOverridden(Env(uiLanguage)));

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

    [Fact]
    public void The_System_Choice_For_An_Invariant_Display_Language_Is_British() =>
        Assert.Equal("en-GB", UiLanguage.SystemChoice(CultureInfo.InvariantCulture, CultureInfo.InvariantCulture).Code);

    // The system default row names the language an empty ui-language gives.
    [Theory]
    [InlineData("de-DE", "de-DE", "de")]
    [InlineData("fr-CA", "en-CA", "fr")]
    [InlineData("en-US", "en-GB", "en-GB")]
    [InlineData("en-US", "en-US", "en-US")]
    [InlineData("en-US", "de-DE", "en-US")]
    [InlineData("en-AU", "en-AU", "en-GB")]
    [InlineData("sv-SE", "sv-SE", "en-GB")]
    [InlineData("pt-PT", "pt-PT", "en-GB")]
    public void The_System_Choice_Is_The_Translation_The_System_Gets(string display, string region, string expected) =>
        Assert.Equal(expected, UiLanguage.SystemChoice(CultureInfo.GetCultureInfo(display), CultureInfo.GetCultureInfo(region)).Code);
}
