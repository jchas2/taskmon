namespace Task.Monitor.System.Services.Tests.Process;

// ProcessService drives WindowsServiceLookup, whose pid to service map and refresh counters are
// static and therefore shared. These classes must not run concurrently or they will step on each
// other's cadence.
[CollectionDefinition(Name)]
public sealed class ProcessServiceCollection
{
    public const string Name = "ProcessService";
}
