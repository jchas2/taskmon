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
    public const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
    public const uint SERVICE_STOP              = 0x00000020;
    public const uint SERVICE_WIN32             = 0x00000030;
    public const uint SERVICE_ALL_ACCESS        = 0xF01FF;
    
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
    
    public const uint DELETE = 0x00010000;      

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ENUM_SERVICE_STATUS_PROCESS
    {
        public string lpServiceName;
        public string lpDisplayName;
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

    [DllImport(Libraries.Advapi32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseServiceHandle(IntPtr hSCObject);

    [DllImport(Libraries.Advapi32, SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr CreateService(
        IntPtr hSCManager,
        string lpServiceName,
        string lpDisplayName,
        uint dwDesiredAccess,
        uint dwServiceType,
        uint dwStartType,
        uint dwErrorControl,
        string lpBinaryPathName,
        string lpLoadOrderGroup,
        string lpdwTagId,
        string lpDependencies,
        string lpServiceStartName,
        string lpPassword);

    [DllImport(Libraries.Advapi32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteService(IntPtr hService);

    [DllImport(Libraries.Advapi32, EntryPoint = "EnumServicesStatusExW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumServicesStatusEx(
        IntPtr hSCManager,
        uint InfoLevel,
        uint dwServiceType,
        uint dwServiceState,
        IntPtr lpServices,
        uint cbBufSize,
        out uint pcbBytesNeeded,
        out uint lpServicesReturned,
        ref uint lpResumeHandle,
        string pszGroupName);
    
    [DllImport(Libraries.Advapi32, SetLastError = true)]
    public static extern IntPtr OpenSCManager(
        string lpMachineName,
        string lpDatabaseName,
        int dwDesiredAccess);

    [DllImport(Libraries.Advapi32, SetLastError = true)]
    public static extern IntPtr OpenService(
        IntPtr hSCManager,
        string lpServiceName,
        int dwDesiredAccess);

    [DllImport(Libraries.Advapi32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryServiceStatusEx(
        IntPtr hService,
        int InfoLevel,
        IntPtr lpBuffer,
        uint cbBufSize,
        out uint pcbBytesNeeded);
}