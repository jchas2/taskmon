using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Task.Monitor.Tests;

// UI text comes from Strings.resx, so it follows the UI culture. Pinning it to the British
// default keeps the tests that assert on screen text passing on any machine, whatever its
// display language.
internal static class UiCulture
{
    internal const string British             = "en-GB";
    internal const string American            = "en-US";
    internal const string BrazilianPortuguese = "pt-BR";
    internal const string Danish              = "da-DK";
    internal const string Dutch               = "nl-NL";
    internal const string French              = "fr-FR";
    internal const string German              = "de-DE";
    internal const string Italian             = "it-IT";
    internal const string Polish              = "pl-PL";
    internal const string Spanish             = "es-ES";

    private sealed class Scope : IDisposable
    {
        private readonly CultureInfo previous = CultureInfo.CurrentUICulture;

        public Scope(CultureInfo culture) => CultureInfo.CurrentUICulture = culture;

        public void Dispose() => CultureInfo.CurrentUICulture = previous;
    }

    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255", Justification = "Meant for apps rather than libraries, but this is a test assembly.")]
    internal static void PinToBritish() =>
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(British);

    // Switches the UI culture for the calling test only, e.g. using var _ = UiCulture.Use("en-US").
    internal static IDisposable Use(string name) => new Scope(CultureInfo.GetCultureInfo(name));
}
