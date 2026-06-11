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
    
    public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PROCESSENTRY32W
    {
        public uint    dwSize;               
        public uint    cntUsage;             
        public uint    th32ProcessID;        
        public UIntPtr th32DefaultHeapID;   
        public uint    th32ModuleID;         
        public uint    cntThreads;           
        public uint    th32ParentProcessID;  
        public int     pcPriClassBase;       
        public uint    dwFlags;              
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
        public string  szExeFile;            
    }

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);
    
    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern bool GetProcessTimes(
        IntPtr    hProcess,
        out MinWinBase.FILETIME lpCreationTime,
        out MinWinBase.FILETIME lpExitTime,
        out MinWinBase.FILETIME lpKernelTime,
        out MinWinBase.FILETIME lpUserTime);
    
    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern IntPtr OpenProcess(
        uint dwDesiredAccess, 
        bool bInheritHandle, 
        uint dwProcessId);

    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool Process32FirstW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool Process32NextW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageNameW(
        IntPtr hProcess,
        uint dwFlags,
        StringBuilder lpExeName,
        ref uint lpdwSize);
}