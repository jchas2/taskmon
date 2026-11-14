using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Task.Monitor.Resources;

internal static class UiLanguage
{
    internal const string UiLanguageEnvVar = "TASKMON_UI_LANGUAGE";

    private const string English = "en";

    private static readonly CultureInfo SystemUiCulture = CultureInfo.CurrentUICulture;

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

    private static bool IsEnglish(CultureInfo culture) =>
        string.Equals(culture.TwoLetterISOLanguageName, English, StringComparison.OrdinalIgnoreCase);

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
