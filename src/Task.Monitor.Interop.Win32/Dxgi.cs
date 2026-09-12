using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

/// <summary>
/// DXGI is bound through its raw vtable rather than through [ComImport] interfaces.
///
/// taskmon publishes with PublishAot=true, and Native AOT has no built-in COM marshalling:
/// Marshal.GetObjectForIUnknown throws "COM Interop requires ComWrapper instance registered
/// for marshalling" regardless of the BuiltInComInteropSupport switch. An RCW based binding
/// therefore works under a Debug build and fails silently in the published build. Calling the
/// vtable directly behaves identically either way.
///
/// Marshal.AddRef, Marshal.Release and Marshal.QueryInterface are raw pointer operations
/// rather than marshalling, so they remain safe to use on the pointers returned here.
/// </summary>
public static unsafe class Dxgi
{
    public const uint DXGI_ADAPTER_FLAG_SOFTWARE = 2;

    public static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
    public static readonly Guid IID_IDXGIAdapter1 = new("29038f61-3839-4626-91fd-086879011a05");
    public static readonly Guid IID_IDXGIAdapter3 = new("645967a4-1392-4310-a798-8053ce3e93fd");

    // Vtable slots counting from IUnknown, whose QueryInterface, AddRef and Release take 0 - 2.
    //
    //   IDXGIObject    SetPrivateData 3, SetPrivateDataInterface 4, GetPrivateData 5, GetParent 6
    //   IDXGIFactory   EnumAdapters 7, MakeWindowAssociation 8, GetWindowAssociation 9,
    //                  CreateSwapChain 10, CreateSoftwareAdapter 11
    //   IDXGIFactory1  EnumAdapters1 12, IsCurrent 13
    private const int SlotEnumAdapters1 = 12;

    //   IDXGIObject    as above, 3 - 6
    //   IDXGIAdapter   EnumOutputs 7, GetDesc 8, CheckInterfaceSupport 9
    //   IDXGIAdapter1  GetDesc1 10
    //   IDXGIAdapter2  GetDesc2 11
    //   IDXGIAdapter3  RegisterHardwareContentProtectionTeardownStatusEvent 12,
    //                  UnregisterHardwareContentProtectionTeardownStatusEvent 13,
    //                  QueryVideoMemoryInfo 14, SetVideoMemoryReservation 15,
    //                  RegisterVideoMemoryBudgetChangeNotificationEvent 16,
    //                  UnregisterVideoMemoryBudgetChangeNotificationEvent 17
    private const int SlotGetDesc1 = 10;
    private const int SlotQueryVideoMemoryInfo = 14;

    public enum DXGI_MEMORY_SEGMENT_GROUP
    {
        LOCAL = 0,     // Dedicated VRAM
        NON_LOCAL = 1  // Shared System RAM
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_QUERY_VIDEO_MEMORY_INFO
    {
        public ulong Budget;
        public ulong CurrentUsage;
        public ulong AvailableForReservation;
        public ulong CurrentReservation;
    }

    // Description is a fixed buffer rather than a marshalled string so the struct stays blittable
    // and can be written to directly by the vtable call.
    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_ADAPTER_DESC1
    {
        public fixed char Description[128];
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public long AdapterLuid;
        public uint Flags;

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

    /// <summary>IDXGIFactory1::EnumAdapters1. Returns DXGI_ERROR_NOT_FOUND once the index is past the last adapter.</summary>
    public static int EnumAdapters1(nint factory, uint adapterIndex, out nint adapter)
    {
        adapter = nint.Zero;

        fixed (nint* ppAdapter = &adapter) {
            return ((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)
                Vtbl(factory)[SlotEnumAdapters1])(factory, adapterIndex, ppAdapter);
        }
    }

    /// <summary>IDXGIAdapter1::GetDesc1.</summary>
    public static int GetDesc1(nint adapter, out DXGI_ADAPTER_DESC1 desc)
    {
        desc = default;

        fixed (DXGI_ADAPTER_DESC1* pDesc = &desc) {
            return ((delegate* unmanaged[Stdcall]<nint, DXGI_ADAPTER_DESC1*, int>)
                Vtbl(adapter)[SlotGetDesc1])(adapter, pDesc);
        }
    }

    /// <summary>
    /// IDXGIAdapter3::QueryVideoMemoryInfo. Note that CurrentUsage is the *calling process'*
    /// video memory usage, not the adapter's, so it reads zero unless the caller has created
    /// D3D resources of its own. Adapter wide usage comes from <see cref="D3DKmt"/> instead.
    /// The pointer must have been obtained by querying for IID_IDXGIAdapter3.
    /// </summary>
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
