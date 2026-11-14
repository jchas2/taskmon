using Task.Monitor.Resources;

namespace Task.Monitor.Gui.Tips;

public static class TipCatalogue
{
    private const char KeyEnd   = '}';
    private const char KeyStart = '{';

    // Splits a tip into plain text and key segments, dropping the braces.
    internal static IEnumerable<(string Text, bool IsKey)> Segments(string tip)
    {
        int start = 0;

        while (start < tip.Length) {
            int keyStart = tip.IndexOf(KeyStart, start);
            int keyEnd   = keyStart < 0 ? -1 : tip.IndexOf(KeyEnd, keyStart + 1);

            if (keyStart < 0 || keyEnd < 0) {
                yield return (tip[start..], false);
                yield break;
            }

            if (keyStart > start) {
                yield return (tip[start..keyStart], false);
            }

            yield return (tip[(keyStart + 1)..keyEnd], true);
            start = keyEnd + 1;
        }
    }

    public static IReadOnlyList<string> Tips => [
        Strings.TipMenuNavigation,
        Strings.TipSummaryPanes,
        Strings.TipPaging,
        Strings.TipFilter,
        Strings.TipSortColumn,
        Strings.TipSortDirection,
        Strings.TipSortShortcuts,
        Strings.TipFreeze,
        Strings.TipIrix,
        Strings.TipMultiSelect,
        Strings.TipUntick,
        Strings.TipEndTask,
        Strings.TipRefresh,
        Strings.TipSetupOptions,
        Strings.TipNextTheme,
        Strings.TipNextLayout,
        Strings.TipLayouts,
        Strings.TipHelpScreen,
        Strings.TipHealth,
        Strings.TipHealthShowProcess,
        Strings.TipNextTip,
        Strings.TipHideTips,
#if __WIN32__
        Strings.TipWindowsServices,
        Strings.TipStartupToggle,
        Strings.TipServicesToggle,
#elif __APPLE__
        Strings.TipDaemons,
        Strings.TipStartupToggle,
#endif
    ];

    internal static string VisibleText(string tip) =>
        string.Concat(Segments(tip).Select(segment => segment.Text));
}
