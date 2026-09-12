namespace Task.Monitor.System.Services.Drivers;

public partial class DriversService
{
#if __WIN32__
    private partial DriversSpecs ScanDrivers()
    {
        DriversSpecs specs = new();

        specs.Drivers.AddRange(DriverLookup.GetDrivers());

        specs.Drivers.Sort(static (left, right) =>
            string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));

        return specs;
    }
#endif
}
