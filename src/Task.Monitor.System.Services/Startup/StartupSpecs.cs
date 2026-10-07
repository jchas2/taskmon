namespace Task.Monitor.System.Services.Startup;

public sealed class StartupSpecs
{
    public List<StartupEntry> Entries { get; set; } = new();
    public List<string>       Notes   { get; set; } = new();
}
