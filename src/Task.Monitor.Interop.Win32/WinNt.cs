using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class WinNt
{
    [StructLayout(LayoutKind.Sequential)]
    public struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct SID_AND_ATTRIBUTES
    {
        public uint* Sid;
        public uint  Attributes;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct TOKEN_USER
    {
        public SID_AND_ATTRIBUTES sidAndAttributes;
    }
    
    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern unsafe bool GetProcessIoCounters(nint hProcess, IO_COUNTERS* counters);
}
