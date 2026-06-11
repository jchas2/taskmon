using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static unsafe class SysInfoApi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        
        public MEMORYSTATUSEX() {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_INFO
    {
        public ushort wProcessorArchitecture;
        public ushort wReserved;
        public uint dwPageSize;
        public uint* lpMinimumApplicationAddress;
        public uint* lpMaximumApplicationAddress;
        public uint* dwActiveProcessorMask;
        public uint dwNumberOfProcessors;
        public uint dwProcessorType;
        public uint dwAllocationGranularity;
        public ushort wProcessorLevel;
        public ushort wProcessorRevision;
    }

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern void GetSystemInfo(SYSTEM_INFO* lpSystemInfo);
    
    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern bool GlobalMemoryStatusEx(MEMORYSTATUSEX* lpBuffer);
}
