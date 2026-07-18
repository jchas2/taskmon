using System.Runtime.Versioning;
using System.ServiceProcess;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Process;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Tests.Process;

public class ServiceUtilsTests
{
#if __WIN32__
    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Should_Get_Services()
    {
        ProcessService processService = new();
        
        ServiceInfo[] serviceInfos = ServiceUtils.GetServices();
        Assert.NotNull(serviceInfos);
        Assert.NotEmpty(serviceInfos);
    }
    
    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Should_Get_Services_With_Pid()
    {
        ProcessService processService = new();
        
        bool foundAnyService = processService.GetProcesses()
            .Select(p => ServiceUtils.GetService(p.Pid, out _))
            .Any();

        Assert.True(foundAnyService);
    }
    
    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Should_Get_Services_With_ImagePath()
    {
        ProcessService processService = new();

        bool foundAnyService = processService.GetProcesses()
            .Select(p => {
                if (ServiceUtils.GetService(p.Pid, out ServiceInfo? si)) {
                    return si;
                }
                return null; 
            })
            .Where(sc => sc != null)
            .Any(sc => !string.IsNullOrEmpty(ServiceUtils.GetServiceImagePath(sc!.ServiceName)));

        Assert.True(foundAnyService);
    }
#endif
}

