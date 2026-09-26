using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

// DXGI is bound through its raw vtable rather than through [ComImport] interfaces.
// taskmon publishes with PublishAot=true, and Native AOT has no built-in COM marshalling.
// We must call the vtable directly.
public static unsafe class Dxgi
{
    public const uint DXGI_ADAPTER_FLAG_SOFTWARE = 2;

    public static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
    public static readonly Guid IID_IDXGIAdapter1 = new("29038f61-3839-4626-91fd-086879011a05");
    public static readonly Guid IID_IDXGIAdapter3 = new("645967a4-1392-4310-a798-8053ce3e93fd");

    // Vtable slots.
    private const int SlotEnumAdapters1 = 12;
    private const int SlotGetDesc1 = 10;
    private const int SlotQueryVideoMemoryInfo = 14;

    public enum DXGI_MEMORY_SEGMENT_GROUP
    {
        LOCAL = 0,     // Dedicated VRAM.
        NON_LOCAL = 1  // Shared System RAM.
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_QUERY_VIDEO_MEMORY_INFO
    {
        public ulong Budget;
        public ulong CurrentUsage;
        public ulong AvailableForReservation;
        public ulong CurrentReservation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_ADAPTER_DESC1
    {
        public fixed char Description[128];
        public uint  VendorId;
        public uint  DeviceId;
        public uint  SubSysId;
        public uint  Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public long  AdapterLuid;
        public uint  Flags;

        public string GetDescription()
        {
            fixed (char* description = Description) {
                return new string(description);
            }
        }
    }

    [DllImport(Libraries.Dxgi, ExactSpelling = true, PreserveSig = true)]
    public static extern int CreateDXGIFactory1(
        [In] ref Guid riid,
        [Out] out nint ppFactory);

    public static int EnumAdapters1(nint factory, uint adapterIndex, out nint adapter)
    {
        adapter = nint.Zero;

        fixed (nint* ppAdapter = &adapter) {
            return ((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)
                Vtbl(factory)[SlotEnumAdapters1])(factory, adapterIndex, ppAdapter);
        }
    }

    public static int GetDesc1(nint adapter, out DXGI_ADAPTER_DESC1 desc)
    {
        desc = default;

        fixed (DXGI_ADAPTER_DESC1* pDesc = &desc) {
            return ((delegate* unmanaged[Stdcall]<nint, DXGI_ADAPTER_DESC1*, int>)
                Vtbl(adapter)[SlotGetDesc1])(adapter, pDesc);
        }
    }

    public static int QueryVideoMemoryInfo(
        nint adapter3,
        uint nodeIndex,
        DXGI_MEMORY_SEGMENT_GROUP segmentGroup,
        out DXGI_QUERY_VIDEO_MEMORY_INFO videoMemoryInfo)
    {
        videoMemoryInfo = default;

        fixed (DXGI_QUERY_VIDEO_MEMORY_INFO* pInfo = &videoMemoryInfo) {
            return ((delegate* unmanaged[Stdcall]<nint, uint, int, DXGI_QUERY_VIDEO_MEMORY_INFO*, int>)
                Vtbl(adapter3)[SlotQueryVideoMemoryInfo])(adapter3, nodeIndex, (int)segmentGroup, pInfo);
        }
    }

    private static void** Vtbl(nint pUnknown) => *(void***)pUnknown;
}
