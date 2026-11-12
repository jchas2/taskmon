#if __APPLE__
using System.Diagnostics;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services;

// IOKit matching notifications for each class, one for a service appearing and one for it going,
// delivered on a serial dispatch queue of our own.
public sealed unsafe partial class DeviceChangeNotifier
{
    private const string QueueLabel = "taskmon.devicechange";

    private sealed class Registration
    {
        public required DeviceChangeNotifier Owner;
        public DeviceCategory                Category;
        public uint                          FirstMatch;
        public uint                          Terminated;
        public GCHandle                      Self;
    }

    private readonly List<Registration> registrations = new();
    private readonly Lock               @lock         = new();

    private nint port;
    private nint queue;
    private bool started;

    // A notification is only armed once its iterator has been read to the end. At registration the
    // first-match iterator holds every service already present, so draining it there skips them.
    private static void Drain(uint iterator)
    {
        uint service;

        while ((service = IOKit.IOIteratorNext((nint)iterator)) != 0) {
            IOKit.IOObjectRelease(service);
        }
    }

    [UnmanagedCallersOnly]
    private static void Noop(nint context) { }

    [UnmanagedCallersOnly]
    private static void NotificationCallback(nint refCon, uint iterator)
    {
        try {
            // Drained first: an undrained iterator delivers no further notifications.
            Drain(iterator);

            if (refCon != nint.Zero && GCHandle.FromIntPtr(refCon).Target is Registration registration) {
                registration.Owner.RaiseDeviceChanged(registration.Category);
            }
        }
        catch (Exception ex) {
            TraceEx.WriteLineOnce(nameof(NotificationCallback), ex.ToString());
        }
    }

    partial void OnStartPlatform()
    {
        lock (@lock) {
            if (started) {
                return;
            }

            started = true;

            port = IOKit.IONotificationPortCreate(mainPort: 0);

            if (port == nint.Zero) {
                Trace.WriteLine($"{nameof(DeviceChangeNotifier)}: {nameof(IOKit.IONotificationPortCreate)} FAILED");
                return;
            }

            queue = Dispatch.dispatch_queue_create(QueueLabel, attributes: nint.Zero);

            if (queue == nint.Zero) {
                Trace.WriteLine($"{nameof(DeviceChangeNotifier)}: {nameof(Dispatch.dispatch_queue_create)} FAILED");

                IOKit.IONotificationPortDestroy(port);
                port = nint.Zero;
                return;
            }

            IOKit.IONotificationPortSetDispatchQueue(port, queue);

            List<string> registered = new();

            Register(DeviceCategory.Storage, "IOMedia",              "partition", registered);
            Register(DeviceCategory.Storage, "IOBlockStorageDriver", "disk",      registered);
            Register(DeviceCategory.Network, "IONetworkInterface",   "network",   registered);
            Register(DeviceCategory.Gpu,     "IOAccelerator",        "GPU",       registered);

            if (registered.Count > 0) {
                Trace.WriteLine(
                    $"{nameof(DeviceChangeNotifier)}: {nameof(IOKit.IOServiceAddMatchingNotification)} OK for " +
                    $"{string.Join(", ", registered)}");
            }
        }
    }

    // Order matters: stop new notifications, wait for any callback already queued, and only then free
    // the handles those callbacks read.
    partial void OnStopPlatform()
    {
        lock (@lock) {
            if (!started) {
                return;
            }

            started = false;

            foreach (Registration registration in registrations) {
                ReleaseIterators(registration);
            }

            if (port != nint.Zero) {
                IOKit.IONotificationPortDestroy(port);
                port = nint.Zero;
            }

            if (queue != nint.Zero) {
                Dispatch.dispatch_sync_f(queue, nint.Zero, &Noop);
                Dispatch.dispatch_release(queue);
                queue = nint.Zero;
            }

            foreach (Registration registration in registrations) {
                if (registration.Self.IsAllocated) {
                    registration.Self.Free();
                }
            }

            registrations.Clear();
        }
    }

    private void Register(DeviceCategory category, string serviceClass, string name, List<string> registered)
    {
        Registration registration = new() {
            Owner = this, Category = category
        };

        registration.Self = GCHandle.Alloc(registration);

        nint refCon = GCHandle.ToIntPtr(registration.Self);

        int result = IOKit.IOServiceAddMatchingNotification(
            port,
            IOKit.FirstMatchNotification,
            IOKit.IOServiceMatching(serviceClass),
            &NotificationCallback,
            refCon,
            out registration.FirstMatch);

        if (result == 0) {
            result = IOKit.IOServiceAddMatchingNotification(
                port,
                IOKit.TerminatedNotification,
                IOKit.IOServiceMatching(serviceClass),
                &NotificationCallback,
                refCon,
                out registration.Terminated);
        }

        if (result != 0) {
            // Neither iterator was drained, so no callback can be running for this registration.
            ReleaseIterators(registration);
            registration.Self.Free();

            Trace.WriteLine(
                $"{nameof(DeviceChangeNotifier)}: {nameof(IOKit.IOServiceAddMatchingNotification)} {name} FAILED " +
                $"{ErrorCodes.DescribeKernReturn(result)}");

            return;
        }

        Drain(registration.FirstMatch);
        Drain(registration.Terminated);

        registrations.Add(registration);
        registered.Add(name);
    }

    private static void ReleaseIterators(Registration registration)
    {
        if (registration.FirstMatch != 0) {
            IOKit.IOObjectRelease(registration.FirstMatch);
            registration.FirstMatch = 0;
        }

        if (registration.Terminated != 0) {
            IOKit.IOObjectRelease(registration.Terminated);
            registration.Terminated = 0;
        }
    }
}
#endif
