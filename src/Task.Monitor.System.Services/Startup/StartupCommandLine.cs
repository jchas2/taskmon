namespace Task.Monitor.System.Services.Startup;

// Splits a stored startup command into its executable path and argument tail. Handles the common
// forms: a quoted path, an unquoted path (with or without spaces), and rundll32-style
// "host.exe target,Entry" invocations. Environment variables are expanded first.
public static class StartupCommandLine
{
    public static (string Path, string Arguments) Split(string? command)
    {
        string expanded = Environment.ExpandEnvironmentVariables(command?.Trim() ?? string.Empty);

        if (expanded.Length == 0) {
            return (string.Empty, string.Empty);
        }

        if (expanded[0] == '"') {
            int closingQuote = expanded.IndexOf('"', 1);

            return closingQuote > 0
                ? (expanded[1..closingQuote], expanded[(closingQuote + 1)..].TrimStart())
                : (expanded[1..], string.Empty);
        }

        // Unquoted: the path may itself contain spaces. Walk the space boundaries and stop at the
        // first prefix that names a file that exists.
        int searchFrom = 0;

        while (true) {
            int space = expanded.IndexOf(' ', searchFrom);

            if (space < 0) {
                break;
            }

            string candidate = expanded[..space];

            if (LooksLikeExistingFile(candidate)) {
                return (candidate, expanded[(space + 1)..].TrimStart());
            }

            searchFrom = space + 1;
        }

        // No prefix resolved: fall back to a plain first-space split.
        int firstSpace = expanded.IndexOf(' ');

        return firstSpace < 0
            ? (expanded, string.Empty)
            : (expanded[..firstSpace], expanded[(firstSpace + 1)..].TrimStart());
    }

    private static bool LooksLikeExistingFile(string path)
    {
        try {
            return File.Exists(path) || File.Exists(path + ".exe");
        }
        catch {
            return false;
        }
    }
}
