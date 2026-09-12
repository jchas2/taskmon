using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.WindowsServices;

public sealed class WindowsServicesSpecs
{
    public List<WindowsServiceInfo> Services { get; set; } = new();
}
