using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

// Plug and play device notifications through CfgMgr32.
public static unsafe class CfgMgr32
{
    public const int CR_SUCCESS = 0;

    public const int CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE = 0;

    public const int CM_NOTIFY_ACTION_DEVICEINTERFACEARRIVAL = 0;
    public const int CM_NOTIFY_ACTION_DEVICEINTERFACEREMOVAL = 1;

    public static readonly Guid GUID_DEVINTERFACE_VOLUME          = new("53f5630d-b6bf-11d0-94f2-00a0c91efb8b");
    public static readonly Guid GUID_DEVINTERFACE_DISK            = new("53f56307-b6bf-11d0-94f2-00a0c91efb8b");
    public static readonly Guid GUID_DEVINTERFACE_NET             = new("cac88484-7515-4c03-82e6-71a87abac361");
    public static readonly Guid GUID_DEVINTERFACE_DISPLAY_ADAPTER = new("5b45201d-f2f2-4f3b-85bb-30ff1f953599");

    [StructLayout(LayoutKind.Sequential)]
    public struct CM_NOTIFY_FILTER
    {
        public uint cbSize;
        public uint Flags;
        public int  FilterType;
        public uint Reserved;
        public Guid ClassGuid;
        private fixed byte UnionPadding[384];
    }

    [DllImport(Libraries.CfgMgr32, EntryPoint = "CM_Register_Notification")]
    public static extern int CM_Register_Notification(
        CM_NOTIFY_FILTER* pFilter,
        nint              pContext,
        delegate*         unmanaged<nint, nint, int, nint, uint, uint> pCallback,
        nint*             pNotifyContext);

    [DllImport(Libraries.CfgMgr32, EntryPoint = "CM_Unregister_Notification")]
    public static extern int CM_Unregister_Notification(nint NotifyContext);
}
