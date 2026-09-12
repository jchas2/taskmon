using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class WinNt
{
    // GetLogicalProcessorInformationEx relationship selector / record discriminator.
    public enum LOGICAL_PROCESSOR_RELATIONSHIP : uint
    {
        RelationProcessorCore    = 0,
        RelationNumaNode         = 1,
        RelationCache            = 2,
        RelationProcessorPackage = 3,
        RelationGroup            = 4,
        RelationProcessorDie     = 5,
        RelationNumaNodeEx       = 6,
        RelationProcessorModule  = 7,
        RelationAll              = 0xffff,
    }

    public enum PROCESSOR_CACHE_TYPE : uint
    {
        CacheUnified     = 0,
        CacheInstruction = 1,
        CacheData        = 2,
        CacheTrace       = 3,
    }

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
