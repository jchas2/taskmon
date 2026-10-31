namespace Task.Monitor.Gui.Tips;

// One-line tips shown in the footer. Keys are marked with braces, e.g. "{z}", and drawn in the
// theme's hotkey colour. Every key here is checked against the code that handles it, not just
// the help screen.
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

    public static IReadOnlyList<string> Tips { get; } = [
        "Press {→} to move from the menu into a view, and {←} to go back to the menu.",
        "On SUMMARY, {←} and {→} move between the panes.",
        "Use {PgUp} and {PgDn} to move a page at a time in any list.",
        "Press {f} to filter the current list; {Esc} clears the filter.",
        "Press {s} to choose the column a process list is sorted on.",
        "Press {a} or {d} to sort a process list ascending or descending.",
        "Press {p}, {g} or {m} to sort processes by CPU, GPU or memory.",
        "Press {z} in a process list to freeze updates; press {z} again to resume.",
        "Press {i} in a process list to toggle Irix mode CPU reporting.",
        "Press {x} for multi-select, {Space} to tick processes, then {k} to end them all.",
        "Press {u} to untick every ticked process.",
        "Press {k} to end the selected process. Its child processes are ended too.",
        "Press {r} or {F5} to refresh the Startup, Apps, Services or Drivers list.",
        "Press {F2} to change the theme, visible columns, metre style and update speed.",
        "In {F2} Setup, LAYOUTS lets you choose or design the SUMMARY layout.",
        "Press {F1} for every key and what the colours mean.",
        "Press {t} to show the next tip.",
        "Hide these tips by unticking Show tips in the footer in {F2} Setup.",
#if __WIN32__
        "Windows services are highlighted in process lists. Switch this off in {F2} Setup.",
#elif __APPLE__
        "Daemons are highlighted in process lists. Switch this off in {F2} Setup.",
#endif
    ];

    internal static string VisibleText(string tip) =>
        string.Concat(Segments(tip).Select(segment => segment.Text));
}
