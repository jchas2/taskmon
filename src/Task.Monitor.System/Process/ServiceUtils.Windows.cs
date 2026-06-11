using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Process;

#pragma warning disable CA1416 // Validate platform compatibility        

public static partial class ServiceUtils
{
#if __WIN32__
    private static readonly Dictionary<int, ServiceController> serviceMap = new();
    private static readonly Lock criticalSection = new();

    private static void BuildServiceMap()
    {
        serviceMap.Clear();

        IntPtr hSCM = WinService.OpenSCManager(null!, null!, WinService.SC_MANAGER_CONNECT);

        if (hSCM == IntPtr.Zero) {
            return;
        }

        ServiceController[] services = ServiceController.GetServices();

        for (int i = 0; i < services.Length; i++) {
            ServiceController service = services[i];

            IntPtr hService = WinService.OpenService(
                hSCM, 
                service.ServiceName, 
                WinService.SERVICE_QUERY_STATUS);

            if (hService == IntPtr.Zero) {
                continue;
            }

            int pid = GetServiceProcessId(hService);

            if (pid == 0) {
                continue;
            }

            serviceMap[pid] = service;
        }

        WinService.CloseServiceHandle(hSCM);
    }

    public static bool GetService(int pid, out ServiceController? service)
    {
        lock (criticalSection) {
            if (serviceMap.Count == 0) {
                BuildServiceMap();
            }

            if (serviceMap.TryGetValue(pid, out service)) {
                return true;
            }

            return false;
        }
    }

    public static string? GetServiceImagePath(string serviceName)
    {
        const string RegPath = @"SYSTEM\CurrentControlSet\Services\";
       
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(RegPath + serviceName);
        return key?.GetValue("ImagePath")?.ToString() ?? null;
    }
    
    private static int GetServiceProcessId(IntPtr hService)
    {
        int pid = 0;
        IntPtr pss = Marshal.AllocHGlobal(Marshal.SizeOf<WinService.SERVICE_STATUS_PROCESS>());

        if (!WinService.QueryServiceStatusEx(
            hService,
            WinService.SC_ENUM_PROCESS_INFO,
            pss,
            (uint)Marshal.SizeOf<WinService.SERVICE_STATUS_PROCESS>(),
            out _)) {
            
            return 0;
        }

        var ssp = Marshal.PtrToStructure<WinService.SERVICE_STATUS_PROCESS>(pss);

        if (ssp.dwCurrentState == (int)ServiceControllerStatus.Running ||
            ssp.dwCurrentState == (int)ServiceControllerStatus.PausePending ||
            ssp.dwCurrentState == (int)ServiceControllerStatus.Paused ||
            ssp.dwCurrentState == (int)ServiceControllerStatus.StartPending ||
            ssp.dwCurrentState == (int)ServiceControllerStatus.StopPending) {

            pid = ssp.dwProcessId;
        }

        Marshal.FreeHGlobal(pss);
        return pid;
    }
    
    public static bool IsService(int pid) => ServiceUtils.GetService(pid, out ServiceController? _);
#endif    
}

#pragma warning restore CA1416 // Validate platform compatibility        
