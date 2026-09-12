namespace Task.Monitor.System.Services.InstalledApps;

public sealed class InstalledApp
{
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public DateTime? InstallDate { get; set; }
    public string? InstallLocation { get; set; }

    // Kilobytes, as the registry stores it (EstimatedSize).
    public long? EstimatedSizeKb { get; set; }

    public string? UninstallCommand { get; set; }
    public InstalledAppScope Scope { get; set; }

    // The registry key path this entry was read from, for the detail line.
    public string Origin { get; set; } = string.Empty;
}
