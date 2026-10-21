namespace Task.Monitor.System.Services.Tests.WindowsServices;

// WindowsServiceLookup's pid to service map and refresh counters are static and therefore shared,
// and ProcessService drives them on every cycle. Tests of either must not run concurrently or they
// will step on each other's cadence.
[CollectionDefinition(Name)]
public sealed class WindowsServiceLookupCollection
{
    public const string Name = "WindowsServiceLookup";
}
