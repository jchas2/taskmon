namespace Task.Monitor.System.Services.InstalledApps;

public sealed class InstalledApp
{
    public string Name              { get; set; } = string.Empty;
    public string? Version          { get; set; }
    public string? Publisher        { get; set; }
    public DateTime? InstallDate    { get; set; }
    public string? InstallLocation  { get; set; }
    public long? EstimatedSizeKb    { get; set; }
    public string? UninstallCommand { get; set; }
    public InstalledAppScope Scope  { get; set; }
    public string Origin            { get; set; } = string.Empty;
}
