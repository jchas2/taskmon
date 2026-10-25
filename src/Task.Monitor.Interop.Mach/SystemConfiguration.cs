using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// SystemConfiguration/SCDynamicStoreCopySpecific.h and SCNetworkConfiguration.h.
public static class SystemConfiguration
{
    private const string DynamicStoreName    = "taskmon";
    private const string PrimaryInterfaceKey = "PrimaryInterface";

    private static readonly string[] GlobalNetworkStateKeys = ["State:/Network/Global/IPv4", "State:/Network/Global/IPv6"];

    public static uint? GetConsoleUserId()
    {
        nint nameRef;
        uint uid;

        try {
            nameRef = SCDynamicStoreCopyConsoleUser(nint.Zero, out uid, out _);
        }
        catch (EntryPointNotFoundException) {
            return null;
        }

        using CFScope name = new(nameRef);

        if (name.IsNull) {
            return null;
        }

        string? userName = CoreFoundation.GetString(name);

        return userName is null or "loginwindow" 
            ? null 
            : uid;
    }

    public static Dictionary<string, string> GetInterfaceDisplayNames()
    {
        Dictionary<string, string> names = new(StringComparer.Ordinal);

        using CFScope interfaces = new(SCNetworkInterfaceCopyAll());

        if (interfaces.IsNull) {
            return names;
        }

        long count = CoreFoundation.CFArrayGetCount(interfaces);

        for (long i = 0; i < count; i++) {
            nint networkInterface = CoreFoundation.CFArrayGetValueAtIndex(interfaces, i);

            if (CoreFoundation.GetString(SCNetworkInterfaceGetBSDName(networkInterface)) is { Length: > 0 } bsdName &&
                CoreFoundation.GetString(SCNetworkInterfaceGetLocalizedDisplayName(networkInterface)) is { Length: > 0 } displayName) {

                names[bsdName] = displayName;
            }
        }

        return names;
    }

    public static string? GetPrimaryInterface()
    {
        using CFScope storeName = new(CoreFoundation.CFStringCreate(DynamicStoreName));
        using CFScope store     = new(SCDynamicStoreCreate(nint.Zero, storeName, nint.Zero, nint.Zero));

        if (store.IsNull) {
            return null;
        }

        foreach (string key in GlobalNetworkStateKeys) {
            using CFScope keyName = new(CoreFoundation.CFStringCreate(key));
            using CFScope state   = new(SCDynamicStoreCopyValue(store, keyName));

            if (!state.IsNull &&
                CoreFoundation.ToDictionary(state).TryGetValue(PrimaryInterfaceKey, out nint primary) &&
                CoreFoundation.GetString(primary) is { Length: > 0 } name) {

                return name;
            }
        }

        return null;
    }

    [DllImport(Libraries.SystemConfiguration)]
    private static extern nint SCDynamicStoreCopyConsoleUser(nint store, out uint uid, out uint gid);

    [DllImport(Libraries.SystemConfiguration)]
    private static extern nint SCDynamicStoreCopyValue(nint store, nint key);

    [DllImport(Libraries.SystemConfiguration)]
    private static extern nint SCDynamicStoreCreate(nint allocator, nint name, nint callout, nint context);

    [DllImport(Libraries.SystemConfiguration)]
    private static extern nint SCNetworkInterfaceCopyAll();

    [DllImport(Libraries.SystemConfiguration)]
    private static extern nint SCNetworkInterfaceGetBSDName(nint networkInterface);

    [DllImport(Libraries.SystemConfiguration)]
    private static extern nint SCNetworkInterfaceGetLocalizedDisplayName(nint networkInterface);
}
