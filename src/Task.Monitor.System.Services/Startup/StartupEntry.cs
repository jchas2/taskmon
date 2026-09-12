namespace Task.Monitor.System.Services.Startup;

public sealed class StartupEntry
{
    // The label the entry is registered under: the registry value name, or the shortcut / file
    // name for a Startup folder item.
    public string Name { get; set; } = string.Empty;

    // The command exactly as it runs at logon, environment variables expanded.
    public string Command { get; set; } = string.Empty;

    // The command split into its target and arguments. ExecutablePath is null when it could not be
    // isolated (an unusual command form, or a shortcut with no file-system target).
    public string? ExecutablePath { get; set; }
    public string? Arguments { get; set; }

    // CompanyName from the target's version resource, when it has one.
    public string? Publisher { get; set; }

    public StartupEntrySource Source { get; set; }
    public StartupEntryScope Scope { get; set; }
    public StartupEntryState State { get; set; } = StartupEntryState.Unknown;

    // When State is Disabled, the time it was disabled (UTC); otherwise null.
    public DateTime? DisabledOnUtc { get; set; }

    // The registry key path or folder the entry was read from, for the detail line.
    public string Origin { get; set; } = string.Empty;
}
