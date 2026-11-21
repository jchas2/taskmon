using Task.Monitor.System.Services.WindowsServices;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Services.Process;

public partial class ProcessService
{
#if __WIN32__
    private volatile Dictionary<int, WindowsServiceInfo> servicesByPid = new();

    private WindowsServicesSpecs? rescanRequestedFor;

    internal static bool GetModulesInternal(SysDiag::Process process, out List<ModuleInfo> moduleInfos)
    {
        moduleInfos = new List<ModuleInfo>();

        foreach (SysDiag::ProcessModule module in process.Modules) {
            ModuleInfo moduleInfo = new() {
                ModuleName = module.ModuleName,
                FileName   = module.FileName
            };

            moduleInfos.Add(moduleInfo);
        }

        return true;
    }

    private void MapServices() =>
        servicesByPid = GetLatest<WindowsServicesInfo>() is { } services
            ? WindowsServicesService.MapProcesses(services.Specs.Services)
            : new();

    private void RescanServicesIfStale(ICollection<int> livePids)
    {
        if (GetLatest<WindowsServicesInfo>()?.Specs is not { } specs ||
            specs == rescanRequestedFor ||
            servicesByPid.Keys.All(livePids.Contains)) {

            return;
        }

        rescanRequestedFor = specs;
        RequestRefreshOf<WindowsServicesService>();
    }
#endif
}
