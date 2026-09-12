using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class WinService
{
    public const int  SC_MANAGER_CONNECT    = 0x0001;
    public const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
    public const int SC_MANAGER_ENUMERATE_SERVICE = 0x0004;

    public const uint SERVICE_TYPE_ALL = 0x00000030;
    public const uint SERVICE_STATE_ALL = 0x00000003;

    public const int SC_ENUM_PROCESS_INFO = 0;
    public const uint INFO_LEVEL_STANDARD = 0;

    public const uint SERVICE_ERROR_NORMAL      = 0x00000001;
    public const uint SERVICE_AUTO_START        = 0x00000002;
    public const int  SERVICE_QUERY_STATUS      = 0x0004;
    public const int  SERVICE_QUERY_CONFIG      = 0x0001;
    public const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
    public const uint SERVICE_STOP              = 0x00000020;
    public const uint SERVICE_WIN32             = 0x00000030;
    public const uint SERVICE_ALL_ACCESS        = 0xF01FF;

    // QueryServiceConfig2 dwInfoLevel values.
    public const uint SERVICE_CONFIG_DESCRIPTION            = 1;
    public const uint SERVICE_CONFIG_DELAYED_AUTO_START_INFO = 3;

    public enum ServiceCurrentState : uint
    {
        SERVICE_STOPPED          = 0x00000001,
        SERVICE_START_PENDING    = 0x00000002,
        SERVICE_STOP_PENDING     = 0x00000003,
        SERVICE_RUNNING          = 0x00000004,
        SERVICE_CONTINUE_PENDING = 0x00000005,
        SERVICE_PAUSE_PENDING    = 0x00000006,
        SERVICE_PAUSED           = 0x00000007
    }

    // QUERY_SERVICE_CONFIGW.dwStartType values.
    public enum ServiceStartType : uint
    {
        SERVICE_BOOT_START   = 0x00000000,
        SERVICE_SYSTEM_START = 0x00000001,
        SERVICE_AUTO_START   = 0x00000002,
        SERVICE_DEMAND_START = 0x00000003,
        SERVICE_DISABLED     = 0x00000004
    }

    public const uint DELETE = 0x00010000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ENUM_SERVICE_STATUS_PROCESS
    {
        public string lpServiceName;
        public string lpDisplayName;
        public int    dwServiceType;
        public int    dwCurrentState;
        public int    dwControlsAccepted;
        public int    dwWin32ExitCode;
        public int    dwServiceSpecificExitCode;
        public int    dwCheckPoint;
        public int    dwWaitHint;
        public int    dwProcessId;
        public int    dwServiceFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SERVICE_STATUS_PROCESS
    {
        public int dwServiceType;
        public int dwCurrentState;
        public int dwControlsAccepted;
        public int dwWin32ExitCode;
        public int dwServiceSpecificExitCode;
        public int dwCheckPoint;
        public int dwWaitHint;
        public int dwProcessId;
        public int dwServiceFlags;
    }

    // Returned by QueryServiceConfig. A variable-size structure - the embedded LPWSTR fields are
    // read via the standard two-call sizing pattern (call once with no buffer to learn the size,
    // then again with a buffer of that size).
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct QUERY_SERVICE_CONFIGW
    {
        public int dwServiceType;
        public int dwStartType;
        public int dwErrorControl;
        public string lpBinaryPathName;
        public string lpLoadOrderGroup;
        public int dwTagId;
        public string lpDependencies;
        public string lpServiceStartName;
        public string lpDisplayName;
    }

    // Returned by QueryServiceConfig2 with dwInfoLevel = SERVICE_CONFIG_DESCRIPTION.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SERVICE_DESCRIPTIONW
    {
        public string lpDescription;
    }

    // Returned by QueryServiceConfig2 with dwInfoLevel = SERVICE_CONFIG_DELAYED_AUTO_START_INFO.
    // Only meaningful for a service whose dwStartType is SERVICE_AUTO_START - it is what
    // distinguishes "Automatic" from "Automatic (Delayed Start)" in the Services snap-in.
    [StructLayout(LayoutKind.Sequential)]
    public struct SERVICE_DELAYED_AUTO_START_INFO
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool fDelayedAutostart;
    }

    [DllImport(Libraries.Advapi32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseServiceHandle(nint hSCObject);

    [DllImport(Libraries.Advapi32, EntryPoint = "EnumServicesStatusExW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern unsafe bool EnumServicesStatusEx(
        nint hSCManager,
        uint InfoLevel,
        uint dwServiceType,
        uint dwServiceState,
        nint lpServices,
        uint cbBufSize,
        uint* pcbBytesNeeded,
        uint* lpServicesReturned,
        uint* lpResumeHandle,
        string pszGroupName);
    
    [DllImport(Libraries.Advapi32, SetLastError = true)]
    public static extern nint OpenSCManager(
        string lpMachineName,
        string lpDatabaseName,
        int dwDesiredAccess);

    [DllImport(Libraries.Advapi32, SetLastError = true)]
    public static extern nint OpenService(
        nint   hSCManager,
        string lpServiceName,
        int    dwDesiredAccess);

    [DllImport(Libraries.Advapi32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern unsafe bool QueryServiceStatusEx(
        nint  hService,
        int   InfoLevel,
        nint  lpBuffer,
        uint  cbBufSize,
        uint* pcbBytesNeeded);

    [DllImport(Libraries.Advapi32, EntryPoint = "QueryServiceConfigW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryServiceConfig(
        nint hService,
        nint lpServiceConfig,
        uint cbBufSize,
        out uint pcbBytesNeeded);

    [DllImport(Libraries.Advapi32, EntryPoint = "QueryServiceConfig2W", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryServiceConfig2(
        nint hService,
        uint dwInfoLevel,
        nint lpBuffer,
        uint cbBufSize,
        out uint pcbBytesNeeded);
}
