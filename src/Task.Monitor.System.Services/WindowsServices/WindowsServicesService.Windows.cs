using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.WindowsServices;

public partial class WindowsServicesService
{
#if __WIN32__
    private partial WindowsServicesSpecs ScanServices()
    {
        WindowsServicesSpecs specs = new();

        specs.Services.AddRange(WindowsServiceLookup.GetServices());

        specs.Services.Sort(static (left, right) =>
            string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));

        return specs;
    }
#endif
}
