namespace Task.Monitor.System.Services.Drivers;

public partial class DriversService
{
#if __APPLE__
    private partial IEnumerable<DriverInfo> EnumerateDrivers() => DriverLookup.GetDrivers();
#endif
}
