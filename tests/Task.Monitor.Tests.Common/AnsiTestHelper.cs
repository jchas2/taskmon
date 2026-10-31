using System.Text.RegularExpressions;

namespace Task.Monitor.Tests.Common;

public static class AnsiTestHelper
{
    private static readonly Regex SgrSequence = new(@"\x1b\[[0-9;]*m", RegexOptions.Compiled);

    // Removes SGR colour sequences so tests can assert on the text the terminal shows. Cursor moves
    // are kept, so text from different rows still can't run together.
    public static string StripColours(string text) => SgrSequence.Replace(text, string.Empty);
}
