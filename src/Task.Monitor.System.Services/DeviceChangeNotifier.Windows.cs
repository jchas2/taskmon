#if __WIN32__
using System.Diagnostics;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services;

public sealed unsafe partial class DeviceChangeNotifier
{
    private sealed class Registration
    {
        public required DeviceChangeNotifier Owner;
        public DeviceCategory                Category;
        public nint                          Handle;
        public GCHandle                      Self;
    }

    private readonly List<Registration> registrations = new();
    private readonly Lock               @lock         = new();

    private bool started;

    [UnmanagedCallersOnly]
    private static uint NotificationCallback(
        nint notify,
        nint context,
        int  action,
        nint eventData,
        uint eventDataSize)
    {
        const uint ErrorSuccess = 0;

        if (action != CfgMgr32.CM_NOTIFY_ACTION_DEVICEINTERFACEARRIVAL &&
            action != CfgMgr32.CM_NOTIFY_ACTION_DEVICEINTERFACEREMOVAL) {
            return ErrorSuccess;
        }

        try {
            if (context != nint.Zero && GCHandle.FromIntPtr(context).Target is Registration registration) {
                registration.Owner.RaiseDeviceChanged(registration.Category);
            }
        }
        catch (Exception ex) {
            TraceEx.WriteLineOnce(nameof(NotificationCallback), ex.ToString());
        }

        return ErrorSuccess;
    }

    partial void OnStartPlatform()
    {
        lock (@lock) {
            if (started) {
                return;
            }

            started = true;

            List<string> registered = new();

            Register(DeviceCategory.Storage, CfgMgr32.GUID_DEVINTERFACE_VOLUME,          "volume",          registered);
            Register(DeviceCategory.Storage, CfgMgr32.GUID_DEVINTERFACE_DISK,            "disk",            registered);
            Register(DeviceCategory.Network, CfgMgr32.GUID_DEVINTERFACE_NET,             "network",         registered);
            Register(DeviceCategory.Gpu,     CfgMgr32.GUID_DEVINTERFACE_DISPLAY_ADAPTER, "display adapter", registered);

            if (registered.Count > 0) {
                Trace.WriteLine($"{nameof(DeviceChangeNotifier)}: {nameof(CfgMgr32.CM_Register_Notification)} OK for {string.Join(", ", registered)}");
            }
        }
    }

    partial void OnStopPlatform()
    {
        lock (@lock) {
            if (!started) {
                return;
            }

            started = false;

            foreach (Registration registration in registrations) {
                if (registration.Handle != nint.Zero) {
                    CfgMgr32.CM_Unregister_Notification(registration.Handle);
                }

                if (registration.Self.IsAllocated) {
                    registration.Self.Free();
                }
            }

            registrations.Clear();
        }
    }

    private void Register(DeviceCategory category, Guid interfaceClass, string interfaceName, List<string> registered)
    {
        Registration registration = new() {
            Owner = this, Category = category
        };
        
        registration.Self = GCHandle.Alloc(registration);

        CfgMgr32.CM_NOTIFY_FILTER filter = default;
        filter.cbSize     = (uint)sizeof(CfgMgr32.CM_NOTIFY_FILTER);
        filter.FilterType = CfgMgr32.CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE;
        filter.ClassGuid  = interfaceClass;

        nint handle;

        int result = CfgMgr32.CM_Register_Notification(
            &filter,
            GCHandle.ToIntPtr(registration.Self),
            &NotificationCallback,
            &handle);

        if (result != CfgMgr32.CR_SUCCESS) {
            registration.Self.Free();

            Trace.WriteLine(
                $"{nameof(DeviceChangeNotifier)}: {nameof(CfgMgr32.CM_Register_Notification)} {interfaceName} FAILED, " +
                $"CONFIGRET 0x{result:X}");

            return;
        }

        registration.Handle = handle;
        registrations.Add(registration);
        registered.Add(interfaceName);
    }
}
#endif
