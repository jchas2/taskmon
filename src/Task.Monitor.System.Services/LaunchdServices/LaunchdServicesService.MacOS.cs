namespace Task.Monitor.System.Services.LaunchdServices;

public partial class LaunchdServicesService
{
#if __APPLE__
    private readonly LaunchdScanCache cache = new();

    private partial IEnumerable<LaunchdServiceInfo> EnumerateServices() => LaunchdServiceLookup.GetServices(cache);
#endif
}
