using System.Diagnostics;

namespace Task.Monitor.System.Services.InstalledApps;

public sealed partial class InstalledAppsService : WorkerService
{
    private const int RescanEveryCycles = 40;

    private static readonly TimeSpan SizeBudget = TimeSpan.FromMilliseconds(750);

    private readonly InstalledAppSizeCache sizeCache = new();

    private InstalledAppsSpecs installedAppsSpecs = new();
    
    private int cyclesSinceScan;

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (RescanDue(ref cyclesSinceScan, RescanEveryCycles)) {
            installedAppsSpecs = ScanInstalledApps();
        }
        else {
            installedAppsSpecs = sizeCache.ComputePending(installedAppsSpecs, SizeBudget, cancellationToken);
        }

        Publish(new InstalledAppsInfo { Specs = installedAppsSpecs });
    }

    protected override void OnStart()
    {
        installedAppsSpecs = ScanInstalledApps();
        Trace.WriteLine($"{ServiceName}: {installedAppsSpecs.Apps.Count} apps: " +
            $"{installedAppsSpecs.Apps.Count(app => app.Scope == InstalledAppScope.Machine)} machine, " +
            $"{installedAppsSpecs.Apps.Count(app => app.Scope == InstalledAppScope.User)} user");
    }

    private partial InstalledAppsSpecs ScanInstalledApps();
}
