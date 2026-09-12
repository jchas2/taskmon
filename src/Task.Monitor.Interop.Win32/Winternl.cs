using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class Winternl
{
    public const uint SystemMemoryListInformation = 80;
    public const int STATUS_SUCCESS = 0;
    public const int STATUS_INFO_LENGTH_MISMATCH = unchecked((int)0xC0000004);

    [InlineArray(8)]
    public struct PriorityPageCounts
    {
        private nuint _element0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_MEMORY_LIST_INFORMATION
    {
        public nuint ZeroedPageCount;
        public nuint FreePageCount;
        public nuint ModifiedPageCount;
        public nuint ModifiedNoWritePageCount;
        public nuint BadPageCount;
        public PriorityPageCounts PageCountByPriority;
        public PriorityPageCounts RepurposedPagesByPriority;
        public nuint ModifiedPageCountPageFile;
    }
    
    [DllImport(Libraries.NtDll)]
    public static extern unsafe int NtQuerySystemInformation(
        uint  SystemInformationClass,
        void* SystemInformation,
        uint  SystemInformationLength,
        uint* ReturnLength);
}