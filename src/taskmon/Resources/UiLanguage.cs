using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Task.Monitor.Resources;

internal static class UiLanguage
{
    internal const string UiLanguageEnvVar = "TASKMON_UI_LANGUAGE";

    private const string English = "en";

    private static readonly CultureInfo SystemUiCulture = CultureInfo.CurrentUICulture;

    private static readonly UiLanguageChoice British = new("en-GB", "English (United Kingdom)");

    // British English and every Strings.<code>.resx file, in the order Setup lists them. The
    // names stay out of Strings.resx because they read the same in every language.
    internal static readonly IReadOnlyList<UiLanguageChoice> Choices =
    [
        new("da",    "Dansk"),
        new("de",    "Deutsch"),
        British,
        new("en-US", "English (United States)"),
        new("es",    "Español"),
        new("fr",    "Français"),
        new("it",    "Italiano"),
        new("nl",    "Nederlands"),
        new("pl",    "Polski"),
        new("pt-BR", "Português (Brasil)"),
    ];

    public static void Apply(string? configured = null)
    {
        CultureInfo culture = Resolve(
            Environment.GetEnvironmentVariable,
            configured,
            SystemUiCulture,
            CultureInfo.CurrentCulture) ?? SystemUiCulture;

        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture              = culture;
    }

    // The choice a ui-language value gives, through its parent cultures: it-IT gives Italiano and
    // de-AT gives Deutsch. A value with no translation of its own, such as en-AU, gives none.
    internal static UiLanguageChoice? Find(string? configured)
    {
        if (!TryGetCulture(configured, out CultureInfo? culture)) {
            return null;
        }

        for (; !string.IsNullOrEmpty(culture.Name); culture = culture.Parent) {
            string name = culture.Name;

            if (Choices.FirstOrDefault(choice => string.Equals(choice.Code, name, StringComparison.OrdinalIgnoreCase)) is { } found) {
                return found;
            }
        }

        return null;
    }

    private static bool IsEnglish(CultureInfo culture) =>
        string.Equals(culture.TwoLetterISOLanguageName, English, StringComparison.OrdinalIgnoreCase);

    internal static bool IsOverridden(Func<string, string?> getEnvironmentVariable) =>
        TryGetCulture(getEnvironmentVariable(UiLanguageEnvVar), out _);

    internal static CultureInfo? Resolve(
        Func<string, string?> getEnvironmentVariable,
        string?               configured,
        CultureInfo           displayCulture,
        CultureInfo           regionCulture)
    {
        if (TryGetCulture(getEnvironmentVariable(UiLanguageEnvVar), out CultureInfo? requested)) {
            return requested;
        }

        if (TryGetCulture(configured, out CultureInfo? chosen)) {
            return chosen;
        }

        if (IsEnglish(displayCulture) &&
            IsEnglish(regionCulture) &&
            !string.Equals(displayCulture.Name, regionCulture.Name, StringComparison.OrdinalIgnoreCase)) {
            return CultureInfo.GetCultureInfo(regionCulture.Name);
        }

        return null;
    }

    // The language an empty ui-language gives: the system's, or British English when the system's
    // has no translation.
    internal static UiLanguageChoice SystemChoice() => SystemChoice(SystemUiCulture, CultureInfo.CurrentCulture);

    internal static UiLanguageChoice SystemChoice(CultureInfo displayCulture, CultureInfo regionCulture)
    {
        CultureInfo culture = Resolve(_ => null, null, displayCulture, regionCulture) ?? displayCulture;
        return Find(culture.Name) ?? British;
    }

    private static bool TryGetCulture(string? name, [NotNullWhen(true)] out CultureInfo? culture)
    {
        culture = null;

        if (string.IsNullOrWhiteSpace(name)) {
            return false;
        }

        try {
            culture = CultureInfo.GetCultureInfo(name.Trim());
            return true;
        }
        catch (CultureNotFoundException) {
            return false;
        }
    }
}
