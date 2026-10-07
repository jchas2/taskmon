#if __APPLE__
namespace Task.Monitor.System.Services.Startup;

// Parses a launchd job property list.
public sealed class LaunchdJobDefinition
{
    public string? Label          { get; private set; }
    public string? ExecutablePath { get; private set; }
    public string? Arguments      { get; private set; }
    public string  Command        { get; private set; } = string.Empty;
    public bool    RunAtLoad      { get; private set; }
    public bool    KeepAlive      { get; private set; }
    public bool?   Disabled       { get; private set; }

    public bool StartsAtLoad => RunAtLoad || KeepAlive;

    public static LaunchdJobDefinition? Parse(object? plist, string? bundleRoot = null)
    {
        if (plist is not IReadOnlyDictionary<string, object?> job) {
            return null;
        }

        List<string> programArguments = ReadStrings(job, "ProgramArguments");
        string? program       = ReadString(job, "Program");
        string? bundleProgram = ReadString(job, "BundleProgram");

        if (program is null && bundleProgram is not null && bundleRoot is not null) {
            program = Path.Combine(bundleRoot, bundleProgram);
        }

        string? executablePath = program ?? (programArguments.Count > 0 
            ? programArguments[0] 
            : null);
        
        string arguments = string.Join(' ', programArguments.Skip(1).Select(Quote));

        LaunchdJobDefinition definition = new();
        // Not inline declared to assist with debugging.
        definition.Label          = ReadString(job, "Label");
        definition.ExecutablePath = string.IsNullOrEmpty(executablePath) ? null : executablePath;
        definition.Arguments      = arguments.Length > 0 ? arguments : null;
        definition.Command        = definition.ExecutablePath is null
            ? string.Empty
            : definition.Arguments is null
                ? definition.ExecutablePath
                : $"\"{definition.ExecutablePath}\" {definition.Arguments}";
        definition.RunAtLoad      = job.GetValueOrDefault("RunAtLoad") is true;
        definition.KeepAlive      = IsKeptAliveFromLoad(job.GetValueOrDefault("KeepAlive"));
        definition.Disabled       = job.GetValueOrDefault("Disabled") as bool?;

        return definition;
    }

    private static bool IsKeptAliveFromLoad(object? keepAlive) => keepAlive switch {
        bool alive                                    => alive,
        IReadOnlyDictionary<string, object?> criteria => criteria.ContainsKey("SuccessfulExit"),
        _                                             => false
    };

    private static string? ReadString(IReadOnlyDictionary<string, object?> job, string key) =>
        job.GetValueOrDefault(key) is string { Length: > 0 } value 
            ? value 
            : null;

    private static List<string> ReadStrings(IReadOnlyDictionary<string, object?> job, string key) =>
        job.GetValueOrDefault(key) is IEnumerable<object?> values
            ? values.OfType<string>().ToList()
            : [];

    private static string Quote(string argument) =>
        argument.Length == 0 || argument.Any(char.IsWhiteSpace)
            ? $"\"{argument}\""
            : argument;
}
#endif
