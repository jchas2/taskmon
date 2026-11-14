using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.Resources;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services.Health;
using Task.Monitor.System.Services.LaunchdServices;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.Tests.Resources;

public sealed class StringsTests
{
    // The German and Italian files are for the language, so every region (de-AT, it-CH) shares them.
    private const string GermanFile  = "de";
    private const string ItalianFile = "it";

    // The default (British) file and every culture with its own file.
    public static TheoryData<string> AllCultureFiles() => new() { "", UiCulture.American, GermanFile, ItalianFile };

    [Theory]
    [MemberData(nameof(FullTranslationCultures))]
    public void A_Full_Translation_Has_Every_Entry(string cultureName)
    {
        Dictionary<string, string> translation = Entries(CultureInfo.GetCultureInfo(cultureName));

        Assert.All(Entries(CultureInfo.InvariantCulture).Keys, key =>
            Assert.True(translation.TryGetValue(key, out string? text) && text.Length > 0, $"{cultureName} has no {key}"));
    }

    [Fact]
    public void A_US_English_Entry_Missing_From_Its_File_Falls_Back_To_The_Default()
    {
        using IDisposable _ = UiCulture.Use(UiCulture.American);

        Assert.Equal("Empty", Strings.ResourceManager.GetString("PaneControlType_Empty", CultureInfo.CurrentUICulture));
    }

    [Theory]
    [InlineData("en-AU")]
    [InlineData("en-NZ")]
    [InlineData("en-IE")]
    [InlineData("en-CA")]
    [InlineData("fr-FR")]
    public void Cultures_Without_Their_Own_File_Get_The_British_Default(string cultureName)
    {
        using IDisposable _ = UiCulture.Use(cultureName);

        Assert.Equal("METRES", Strings.SetupMetresMenuItem);
    }

    // Enums the UI shows through ToDisplayText(); each member needs its <Type>_<Member> entry.
    // PaneControlType is covered by LayoutDesignerScreenTests, and only some Statistics members are shown.
    public static TheoryData<Type> DisplayedEnums() => new() {
        typeof(HealthLevel),
        typeof(HealthResource),
        typeof(LaunchdDomain),
        typeof(MemoryPressure),
        typeof(MetreControlStyle),
        typeof(ThermalComponent),
        typeof(ThermalPressure),
        typeof(UpdateInterval)
    };

    private static Dictionary<string, string> Entries(CultureInfo culture)
    {
        ResourceSet? set = Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);

        return set.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }

    // The fixed-width audit: an entry shown in a fixed width (a column, menu or label) has
    // "Max N characters." in its comment in Strings.resx, and every culture's text must fit it.
    [Theory]
    [MemberData(nameof(AllCultureFiles))]
    public void Every_Entry_Fits_Its_Width_Limit(string cultureName)
    {
        Dictionary<string, int> limits = WidthLimits();
        Assert.NotEmpty(limits);

        foreach ((string key, string text) in Entries(CultureInfo.GetCultureInfo(cultureName))) {
            if (limits.TryGetValue(key, out int max)) {
                Assert.True(text.Length <= max, $"{cultureName} {key}: \"{text}\" is {text.Length} characters, over {max}");
            }
        }
    }

    // A <Type>_<Member> entry whose member was renamed would never be read again.
    [Fact]
    public void Every_Enum_Entry_Names_A_Member_Of_Its_Enum()
    {
        Dictionary<string, Type> enumTypes = DisplayedEnums()
            .Select(row => (Type)row[0])
            .Append(typeof(PaneControlType))
            .Append(typeof(Statistics))
            .ToDictionary(type => type.Name);

        foreach (string key in Entries(CultureInfo.InvariantCulture).Keys.Where(key => key.Contains('_'))) {
            string[] parts = key.Split('_', 2);

            Assert.True(enumTypes.TryGetValue(parts[0], out Type? enumType), $"{key}: no enum named {parts[0]}");
            Assert.True(Enum.IsDefined(enumType, parts[1]), $"{key}: {parts[0]} has no member {parts[1]}");
        }
    }

    [Theory]
    [MemberData(nameof(DisplayedEnums))]
    public void Every_Member_Of_A_Displayed_Enum_Has_A_Default_Entry(Type enumType)
    {
        Dictionary<string, string> defaults = Entries(CultureInfo.InvariantCulture);

        Assert.All(Enum.GetNames(enumType), name => Assert.Contains($"{enumType.Name}_{name}", defaults.Keys));
    }

    [Theory]
    [MemberData(nameof(OverrideCultures))]
    public void Every_Override_Keeps_The_Placeholders_And_Key_Markup_Of_The_Default(string cultureName)
    {
        Dictionary<string, string> defaults = Entries(CultureInfo.InvariantCulture);

        foreach ((string key, string text) in Entries(CultureInfo.GetCultureInfo(cultureName))) {
            Assert.True(Tokens(defaults[key]).SequenceEqual(Tokens(text)), $"{cultureName} {key}: \"{text}\"");
        }
    }

    [Theory]
    [MemberData(nameof(OverrideCultures))]
    public void Every_Override_Replaces_A_Default_Entry(string cultureName)
    {
        Dictionary<string, string> defaults = Entries(CultureInfo.InvariantCulture);

        Assert.All(Entries(CultureInfo.GetCultureInfo(cultureName)).Keys, key => Assert.Contains(key, defaults.Keys));
    }

    // An entry whose braces are all {0}-style placeholders goes through string.Format, which throws
    // at runtime on a malformed one. Entries with {F2}-style key markup aren't format strings.
    [Theory]
    [InlineData("")]
    [InlineData(UiCulture.American)]
    [InlineData(GermanFile)]
    [InlineData(ItalianFile)]
    public void Every_Placeholder_Entry_Formats(string cultureName)
    {
        foreach ((string key, string text) in Entries(CultureInfo.GetCultureInfo(cultureName))) {
            List<string> tokens = Tokens(text);

            if (tokens.Count == 0 || !tokens.All(token => Regex.IsMatch(token, @"^\{\d+(:[^{}]*)?\}$"))) {
                continue;
            }

            int      count = tokens.Max(token => int.Parse(Regex.Match(token, @"\d+").Value)) + 1;
            object[] args  = Enumerable.Repeat<object>(1.5, count).ToArray();

            Exception? error = Record.Exception(() => string.Format(CultureInfo.InvariantCulture, text, args));
            Assert.True(error is null, $"{key}: {error?.Message}");
        }
    }

    // Every culture with a full translation: each entry in Strings.resx has one there.
    public static TheoryData<string> FullTranslationCultures() => new() { GermanFile, ItalianFile };

    [Theory]
    [InlineData("de-DE")]
    [InlineData("de-AT")]
    [InlineData("de-CH")]
    public void German_Regions_Share_The_German_File(string cultureName)
    {
        using IDisposable _ = UiCulture.Use(cultureName);

        Assert.Equal("ANZEIGEN", Strings.SetupMetresMenuItem);
        Assert.Equal("Abbrechen", Strings.DialogCancel);
    }

    [Theory]
    [InlineData("it-IT")]
    [InlineData("it-CH")]
    public void Italian_Regions_Share_The_Italian_File(string cultureName)
    {
        using IDisposable _ = UiCulture.Use(cultureName);

        Assert.Equal("INDICATORI", Strings.SetupMetresMenuItem);
        Assert.Equal("Annulla", Strings.DialogCancel);
    }

    // Every culture with its own Strings.<culture>.resx file.
    public static TheoryData<string> OverrideCultures() => new() { UiCulture.American, GermanFile, ItalianFile };

    [Fact]
    public void The_Default_Strings_Are_British() =>
        Assert.Equal("METRES", Entries(CultureInfo.InvariantCulture)[nameof(Strings.SetupMetresMenuItem)]);

    // {0}-style placeholders and {F2}-style key markup.
    private static List<string> Tokens(string text) =>
        Regex.Matches(text, @"\{[^{}]+\}").Select(match => match.Value).Order(StringComparer.Ordinal).ToList();

    [Theory]
    [InlineData(nameof(Strings.SetupMetresMenuItem),          "METRES",          "METERS")]
    [InlineData(nameof(Strings.HelpChartColoursSection),      "CHART COLOURS",   "CHART COLORS")]
    [InlineData(nameof(Strings.HealthUtilisationColumn),      "UTILISATION",     "UTILIZATION")]
    [InlineData(nameof(Strings.PerformanceUtilisationColumn), "Utilisation",     "Utilization")]
    [InlineData(nameof(Strings.LabelVirtualisation),          "Virtualisation:", "Virtualization:")]
    [InlineData(nameof(Strings.DiskSpaceCancellingStatus),    "Cancelling… {0}", "Canceling… {0}")]
    [InlineData("PaneControlType_CpuMetre",                   "Cpu Metre",       "Cpu Meter")]
    public void US_English_Overrides_The_British_Spelling(string key, string british, string american)
    {
        Assert.Equal(british,  Strings.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(UiCulture.British)));
        Assert.Equal(american, Strings.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(UiCulture.American)));
    }

    // The limits come from the comments in a copy of Strings.resx next to the test assembly.
    private static Dictionary<string, int> WidthLimits() =>
        XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Resources", "Strings.resx")).Root!
            .Elements("data")
            .Select(data => (
                Key:   (string)data.Attribute("name")!,
                Limit: Regex.Match((string?)data.Element("comment") ?? string.Empty, @"Max (\d+) characters\.")))
            .Where(entry => entry.Limit.Success)
            .ToDictionary(entry => entry.Key, entry => int.Parse(entry.Limit.Groups[1].Value));
}
