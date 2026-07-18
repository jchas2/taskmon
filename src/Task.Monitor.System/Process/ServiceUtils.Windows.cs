using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Process;

#pragma warning disable CA1416 // Validate platform compatibility        

public static partial class ServiceUtils
{
#if __WIN32__
    private static readonly Dictionary<int, ServiceInfo> serviceMap = new();
    private static readonly Lock criticalSection = new();

    private static void BuildServiceMap()
    {
        serviceMap.Clear();

        IntPtr hSCM = WinService.OpenSCManager(null!, null!, WinService.SC_MANAGER_CONNECT);

        if (hSCM == IntPtr.Zero) {
            Trace.WriteLine($"Failed to open SCM Manager: {PInvokeErrorHelpers.GetFormattedErrorMesage()}");
            return;
        }

        ServiceInfo[] services = GetServices();

        for (int i = 0; i < services.Length; i++) {
            ServiceInfo service = services[i];

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

    public static bool GetService(int pid, out ServiceInfo? service)
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

            Trace.WriteLine($"GetServiceProcessId QueryServiceStatusEx: {PInvokeErrorHelpers.GetFormattedErrorMesage()}");
            return 0;
        }

        var ssp = Marshal.PtrToStructure<WinService.SERVICE_STATUS_PROCESS>(pss);

        if (ssp.dwCurrentState == (int)WinService.ServiceCurrentState.SERVICE_RUNNING ||
            ssp.dwCurrentState == (int)WinService.ServiceCurrentState.SERVICE_PAUSE_PENDING ||
            ssp.dwCurrentState == (int)WinService.ServiceCurrentState.SERVICE_PAUSED ||
            ssp.dwCurrentState == (int)WinService.ServiceCurrentState.SERVICE_START_PENDING ||
            ssp.dwCurrentState == (int)WinService.ServiceCurrentState.SERVICE_STOP_PENDING) {

            pid = ssp.dwProcessId;
        }

        Marshal.FreeHGlobal(pss);
        return pid;
    }

    internal static ServiceInfo[] GetServices()
    {
        ServiceInfo[] services = [];
        IntPtr hSCM = WinService.OpenSCManager(null!, null!, WinService.SC_MANAGER_ENUMERATE_SERVICE);
        
        if (hSCM == IntPtr.Zero) {
            Trace.WriteLine($"Failed to open SCM Manager: {PInvokeErrorHelpers.GetFormattedErrorMesage()}");
            return services;
        }

        IntPtr buffer = IntPtr.Zero;
        
        uint bytesNeeded = 0;
        uint servicesReturned = 0;
        uint resumeHandle = 0;

        WinService.EnumServicesStatusEx(
            hSCM,
            WinService.INFO_LEVEL_STANDARD,
            WinService.SERVICE_TYPE_ALL,
            WinService.SERVICE_STATE_ALL,
            IntPtr.Zero,
            0,
            out bytesNeeded,
            out servicesReturned,
            ref resumeHandle,
            null!);

        if (bytesNeeded == 0) {
            Trace.WriteLine($"EnumServicesStatusEx bytesNeeded returned 0: {PInvokeErrorHelpers.GetFormattedErrorMesage()}");
            WinService.CloseServiceHandle(hSCM);
            return services;
        }
        
        buffer = Marshal.AllocHGlobal((IntPtr)bytesNeeded);
        resumeHandle = 0;

        bool result = WinService.EnumServicesStatusEx(
            hSCM,
            WinService.INFO_LEVEL_STANDARD,
            WinService.SERVICE_TYPE_ALL,
            WinService.SERVICE_STATE_ALL,
            buffer,
            bytesNeeded,
            out bytesNeeded,
            out servicesReturned,
            ref resumeHandle,
            null!);

        if (!result) {
            Trace.WriteLine($"Failed to EnumServicesStatusEx: {PInvokeErrorHelpers.GetFormattedErrorMesage()}");
            
            if (buffer != IntPtr.Zero) {
                Marshal.FreeHGlobal(buffer);
            }

            WinService.CloseServiceHandle(hSCM);
            return services;
        }

        IntPtr currentPtr = buffer;
        int structSize = Marshal.SizeOf<WinService.ENUM_SERVICE_STATUS_PROCESS>();
        services = new ServiceInfo[servicesReturned];
        
        for (int i = 0; i < servicesReturned; i++) {
            var status = Marshal.PtrToStructure<WinService.ENUM_SERVICE_STATUS_PROCESS>(currentPtr);
            
            services[i] = new ServiceInfo() {
                ServiceName = status.lpServiceName,
                DisplayName = status.lpDisplayName
            };

            currentPtr = IntPtr.Add(currentPtr, structSize);
        }

        if (buffer != IntPtr.Zero) {
            Marshal.FreeHGlobal(buffer);
        }
        
        WinService.CloseServiceHandle(hSCM);
        return services;
    }
    
    public static bool IsService(int pid) => ServiceUtils.GetService(pid, out ServiceInfo? _);
#endif    
}

#pragma warning restore CA1416 // Validate platform compatibility        
