using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

/// <summary>
/// Plug and play device notifications through CfgMgr32. CM_Register_Notification delivers device
/// interface arrival and removal on a callback within milliseconds, which is how Task Manager
/// picks up a drive the instant it is inserted rather than on the next poll.
///
/// The callback is a <see cref="System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute"/>
/// static, passed as a function pointer, so this stays AOT safe (taskmon publishes with
/// PublishAot=true).
/// </summary>
public static unsafe class CfgMgr32
{
    // CONFIGRET
    public const int CR_SUCCESS = 0;

    // CM_NOTIFY_FILTER_TYPE
    public const int CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE = 0;

    // CM_NOTIFY_ACTION
    public const int CM_NOTIFY_ACTION_DEVICEINTERFACEARRIVAL = 0;
    public const int CM_NOTIFY_ACTION_DEVICEINTERFACEREMOVAL = 1;

    // Device interface classes. Volume and Disk both fire for storage: Volume covers a mounted
    // partition appearing, Disk covers the physical drive.
    public static readonly Guid GUID_DEVINTERFACE_VOLUME          = new("53f5630d-b6bf-11d0-94f2-00a0c91efb8b");
    public static readonly Guid GUID_DEVINTERFACE_DISK            = new("53f56307-b6bf-11d0-94f2-00a0c91efb8b");
    public static readonly Guid GUID_DEVINTERFACE_NET             = new("cac88484-7515-4c03-82e6-71a87abac361");
    public static readonly Guid GUID_DEVINTERFACE_DISPLAY_ADAPTER = new("5b45201d-f2f2-4f3b-85bb-30ff1f953599");

    /// <summary>
    /// CM_NOTIFY_FILTER is a 16 byte header followed by a 400 byte union whose largest member is
    /// <c>WCHAR InstanceId[MAX_DEVICE_ID_LEN]</c> (200). Only the DeviceInterface.ClassGuid member
    /// is used here; the remainder of the union is left as padding so the struct keeps its exact
    /// 416 byte size, which <c>cbSize</c> has to match.
    /// </summary>
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

    // DWORD CALLBACK PcmNotifyCallback(HCMNOTIFICATION, PVOID Context, CM_NOTIFY_ACTION,
    //                                  PCM_NOTIFY_EVENT_DATA, DWORD EventDataSize)
    [DllImport(Libraries.CfgMgr32, EntryPoint = "CM_Register_Notification")]
    public static extern int CM_Register_Notification(
        CM_NOTIFY_FILTER* pFilter,
        nint pContext,
        delegate* unmanaged<nint, nint, int, nint, uint, uint> pCallback,
        nint* pNotifyContext);

    [DllImport(Libraries.CfgMgr32, EntryPoint = "CM_Unregister_Notification")]
    public static extern int CM_Unregister_Notification(nint NotifyContext);
}
