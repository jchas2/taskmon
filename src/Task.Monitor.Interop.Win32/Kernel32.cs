using System.Runtime.InteropServices;
using System.Text;

namespace Task.Monitor.Interop.Win32;

public static class Kernel32
{
    public const uint TH32CS_SNAPPROCESS = 0x00000002;
    public const int  MAX_PATH           = 260;
    
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint PROCESS_NAME_WIN32  = 0;   
    public const uint PROCESS_NAME_NATIVE = 1;   
    
    public static readonly nint INVALID_HANDLE_VALUE = -1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct PROCESSENTRY32W
    {
        public uint  dwSize;               
        public uint  cntUsage;             
        public uint  th32ProcessID;        
        public nuint th32DefaultHeapID;   
        public uint  th32ModuleID;         
        public uint  cntThreads;           
        public uint  th32ParentProcessID;  
        public int   pcPriClassBase;       
        public uint  dwFlags;              
        public fixed char szExeFile[MAX_PATH];
    }

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern bool CloseHandle(nint hObject);

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern nint CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);
    
    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern unsafe bool GetProcessTimes(
        nint hProcess,
        MinWinBase.FILETIME* lpCreationTime,
        MinWinBase.FILETIME* lpExitTime,
        MinWinBase.FILETIME* lpKernelTime,
        MinWinBase.FILETIME* lpUserTime);
    
    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern nint OpenProcess(
        uint dwDesiredAccess, 
        bool bInheritHandle, 
        uint dwProcessId);

    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern unsafe bool Process32FirstW(nint hSnapshot, PROCESSENTRY32W* lppe);

    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern unsafe bool Process32NextW(nint hSnapshot, PROCESSENTRY32W* lppe);

    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern unsafe bool QueryFullProcessImageNameW(
        nint hProcess,
        uint dwFlags,
        char* lpExeName,
        uint* lpdwSize);
}
