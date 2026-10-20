namespace Task.Monitor.System.Services.Tests.InstalledApps;

// A macOS scan checks every app's code signature and then walks gigabytes of bundle files to
// measure their sizes - enough load to push the timing-sensitive service tests past their windows.
// Disabling parallelization runs this collection on its own, after the parallel tests finish.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class InstalledAppsServiceCollection
{
    public const string Name = "InstalledAppsService";
}
