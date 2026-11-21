using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// SystemConfiguration/SCDynamicStoreCopySpecific.h and SCNetworkConfiguration.h.
public static class SystemConfiguration
{
    public const string GlobalIPv4StateKey  = "State:/Network/Global/IPv4";
    public const string GlobalIPv6StateKey  = "State:/Network/Global/IPv6";
    public const string PrimaryInterfaceKey = "PrimaryInterface";

    [DllImport(Libraries.SystemConfiguration)]
    public static extern nint SCDynamicStoreCopyConsoleUser(nint store, out uint uid, out uint gid);

    [DllImport(Libraries.SystemConfiguration)]
    public static extern nint SCDynamicStoreCopyValue(nint store, nint key);

    [DllImport(Libraries.SystemConfiguration)]
    public static extern nint SCDynamicStoreCreate(nint allocator, nint name, nint callout, nint context);

    [DllImport(Libraries.SystemConfiguration)]
    public static extern nint SCNetworkInterfaceCopyAll();

    [DllImport(Libraries.SystemConfiguration)]
    public static extern nint SCNetworkInterfaceGetBSDName(nint networkInterface);

    [DllImport(Libraries.SystemConfiguration)]
    public static extern nint SCNetworkInterfaceGetLocalizedDisplayName(nint networkInterface);
}
