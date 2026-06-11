using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class IOReport
{
    [DllImport(Libraries.IOReport)]
    public static extern IntPtr IOReportCopyChannelsInGroup(
        IntPtr group,
        IntPtr subgroup,
        ulong a,
        ulong b,
        ulong c);

    [DllImport(Libraries.IOReport)]
    public static extern IntPtr IOReportCreateSubscription(
        IntPtr a,
        IntPtr channels,
        out IntPtr subbedChannels,
        ulong d,
        IntPtr e);

    [DllImport(Libraries.IOReport)]
    public static extern IntPtr IOReportCreateSamples(IntPtr subscription, IntPtr channels, IntPtr unused);

    [DllImport(Libraries.IOReport)]
    public static extern IntPtr IOReportCreateSamplesDelta(IntPtr prev, IntPtr current, IntPtr unused);

    [DllImport(Libraries.IOReport)]
    public static extern IntPtr IOReportChannelGetGroup(IntPtr item);

    [DllImport(Libraries.IOReport)]
    public static extern IntPtr IOReportChannelGetSubGroup(IntPtr item);

    [DllImport(Libraries.IOReport)]
    public static extern IntPtr IOReportChannelGetChannelName(IntPtr item);

    [DllImport(Libraries.IOReport)]
    public static extern int IOReportStateGetCount(IntPtr item);

    [DllImport(Libraries.IOReport)]
    public static extern IntPtr IOReportStateGetNameForIndex(IntPtr item, int index);

    [DllImport(Libraries.IOReport)]
    public static extern long IOReportStateGetResidency(IntPtr item, int index);
}
