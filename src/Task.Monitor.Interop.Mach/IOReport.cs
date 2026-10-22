using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class IOReport
{
    [DllImport(Libraries.IOReport)]
    public static extern nint IOReportChannelGetChannelName(nint item);

    [DllImport(Libraries.IOReport)]
    public static extern nint IOReportChannelGetGroup(nint item);

    [DllImport(Libraries.IOReport)]
    public static extern nint IOReportChannelGetSubGroup(nint item);

    [DllImport(Libraries.IOReport)]
    public static extern nint IOReportChannelGetUnitLabel(nint item);

    [DllImport(Libraries.IOReport)]
    public static extern IntPtr IOReportCopyChannelsInGroup(
        nint  group,
        nint  subgroup,
        ulong a,
        ulong b,
        ulong c);

    [DllImport(Libraries.IOReport)]
    public static extern nint IOReportCreateSamples(nint subscription, nint channels, nint unused);

    [DllImport(Libraries.IOReport)]
    public static extern nint IOReportCreateSamplesDelta(nint prev, nint current, nint unused);

    [DllImport(Libraries.IOReport)]
    public static extern IntPtr IOReportCreateSubscription(
        nint     a,
        nint     channels,
        out nint subbedChannels,
        ulong    d,
        nint     e);

    [DllImport(Libraries.IOReport)]
    public static extern long IOReportSimpleGetIntegerValue(nint item, int index);

    [DllImport(Libraries.IOReport)]
    public static extern int IOReportStateGetCount(nint item);

    [DllImport(Libraries.IOReport)]
    public static extern nint IOReportStateGetNameForIndex(nint item, int index);

    [DllImport(Libraries.IOReport)]
    public static extern long IOReportStateGetResidency(nint item, int index);
}
