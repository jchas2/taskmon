using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;
using Task.Monitor.Cli.Utils;
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

        nint hSCM = WinService.OpenSCManager(null!, null!, WinService.SC_MANAGER_CONNECT);

        if (hSCM == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(WinService.SC_MANAGER_CONNECT)); 
            return;
        }

        ServiceInfo[] services = GetServices();

        for (int i = 0; i < services.Length; i++) {
            ServiceInfo service = services[i];

            nint hService = WinService.OpenService(
                hSCM, 
                service.ServiceName, 
                WinService.SERVICE_QUERY_STATUS);

            if (hService == nint.Zero) {
                PInvokeErrorHelpers.TraceOnceOnLastError($"{nameof(WinService.OpenService)} {service.ServiceName}");
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
        string subKey = $"{RegPath}{serviceName}";
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(subKey);

        if (key == null) {
            TraceEx.WriteLineOnce(subKey, "Failed to open registry key");
        }
        
        return key?.GetValue("ImagePath")?.ToString() ?? null;
    }
    
    private static unsafe int GetServiceProcessId(nint hService)
    {
        int pid = 0;
        nint pss = Marshal.AllocHGlobal(Marshal.SizeOf<WinService.SERVICE_STATUS_PROCESS>());
        uint bytesNeeded = 0;
        
        if (!WinService.QueryServiceStatusEx(
            hService,
            WinService.SC_ENUM_PROCESS_INFO,
            pss,
            (uint)Marshal.SizeOf<WinService.SERVICE_STATUS_PROCESS>(),
            &bytesNeeded)) {

            PInvokeErrorHelpers.TraceOnceOnLastError($"{nameof(WinService.QueryServiceStatusEx)} {hService}");
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

    internal static unsafe ServiceInfo[] GetServices()
    {
        ServiceInfo[] services = [];
        nint hSCM = WinService.OpenSCManager(null!, null!, WinService.SC_MANAGER_ENUMERATE_SERVICE);
        
        if (hSCM == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(WinService.OpenSCManager), 
                $"Failed GetServices {nameof(WinService.SC_MANAGER_ENUMERATE_SERVICE)}");
            
            return services;
        }

        nint buffer = nint.Zero;
        
        uint bytesNeeded = 0;
        uint servicesReturned = 0;
        uint resumeHandle = 0;

        WinService.EnumServicesStatusEx(
            hSCM,
            WinService.INFO_LEVEL_STANDARD,
            WinService.SERVICE_TYPE_ALL,
            WinService.SERVICE_STATE_ALL,
            nint.Zero,
            0,
            &bytesNeeded,
            &servicesReturned,
            &resumeHandle,
            null!);

        if (bytesNeeded == 0) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(WinService.EnumServicesStatusEx), 
                $"{nameof(WinService.EnumServicesStatusEx)} {nameof(bytesNeeded)} returned 0");
            
            WinService.CloseServiceHandle(hSCM);
            return services;
        }
        
        buffer = Marshal.AllocHGlobal((nint)bytesNeeded);
        resumeHandle = 0;

        bool result = WinService.EnumServicesStatusEx(
            hSCM,
            WinService.INFO_LEVEL_STANDARD,
            WinService.SERVICE_TYPE_ALL,
            WinService.SERVICE_STATE_ALL,
            buffer,
            bytesNeeded,
            &bytesNeeded,
            &servicesReturned,
            &resumeHandle,
            null!);

        if (!result) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(WinService.EnumServicesStatusEx), 
                $"{nameof(WinService.EnumServicesStatusEx)} failed to allocate {nameof(buffer)}");
            
            if (buffer != nint.Zero) {
                Marshal.FreeHGlobal(buffer);
            }

            WinService.CloseServiceHandle(hSCM);
            return services;
        }

        nint currentPtr = buffer;
        int structSize = Marshal.SizeOf<WinService.ENUM_SERVICE_STATUS_PROCESS>();
        services = new ServiceInfo[servicesReturned];
        
        for (int i = 0; i < servicesReturned; i++) {
            var status = Marshal.PtrToStructure<WinService.ENUM_SERVICE_STATUS_PROCESS>(currentPtr);
            
            services[i] = new ServiceInfo() {
                ServiceName = status.lpServiceName,
                DisplayName = status.lpDisplayName
            };

            currentPtr = nint.Add(currentPtr, structSize);
        }

        if (buffer != nint.Zero) {
            Marshal.FreeHGlobal(buffer);
        }
        
        WinService.CloseServiceHandle(hSCM);
        return services;
    }
    
    public static bool IsService(int pid) => ServiceUtils.GetService(pid, out ServiceInfo? _);
#endif    
}

#pragma warning restore CA1416 // Validate platform compatibility        
