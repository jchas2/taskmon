using System.Diagnostics;

namespace Task.Monitor.System.Services.Drivers;

public sealed partial class DriversService : WorkerService
{
    private const int RescanEveryCycles = 40;

    private DriversSpecs driversSpecs = new();

    private int cyclesSinceScan;

    private readonly List<string> scanNotes = new();

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (RescanDue(ref cyclesSinceScan, RescanEveryCycles)) {
            driversSpecs = ScanDrivers();
        }

        Publish(new DriversInfo { Specs = driversSpecs });
    }

    protected override void OnStart()
    {
        driversSpecs = ScanDrivers();

        foreach (string note in scanNotes) {
            Trace.WriteLine($"{ServiceName}: {note}");
        }

        Trace.WriteLine(driversSpecs.Drivers.Count > 0
            ? $"{ServiceName}: {driversSpecs.Drivers.Count} drivers"
            : $"{ServiceName}: drivers FAILED, none read, see the error above");
    }

    private DriversSpecs ScanDrivers()
    {
        DriversSpecs specs = new();

        scanNotes.Clear();
#if __APPLE__
        specs.Drivers.AddRange(DriverLookup.GetDrivers(scanNotes));
#else
        specs.Drivers.AddRange(DriverLookup.GetDrivers());
#endif

        specs.Drivers.Sort(static (left, right) =>
            string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));

        return specs;
    }
}
