namespace Task.Monitor.System.Services.Startup;

public sealed class StartupEntry
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; }
    public string? Arguments { get; set; }
    public string? Publisher { get; set; }
    public StartupEntrySource Source { get; set; }
    public StartupEntryScope Scope { get; set; }
    public StartupEntryState State { get; set; } = StartupEntryState.Unknown;
    public DateTime? DisabledOnUtc { get; set; }
     public string Origin { get; set; } = string.Empty;
}
