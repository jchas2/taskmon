#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public static class NetworkConfiguration
{
    private const string DynamicStoreName = "taskmon";

    private static readonly string[] GlobalStateKeys = [
        SystemConfiguration.GlobalIPv4StateKey,
        SystemConfiguration.GlobalIPv6StateKey
    ];

    public static Dictionary<string, string> GetInterfaceDisplayNames()
    {
        Dictionary<string, string> names = new(StringComparer.Ordinal);

        using CFScope interfaces = new(SystemConfiguration.SCNetworkInterfaceCopyAll());

        if (interfaces.IsNull) {
            return names;
        }

        long count = CoreFoundation.CFArrayGetCount(interfaces);

        for (long i = 0; i < count; i++) {
            nint networkInterface = CoreFoundation.CFArrayGetValueAtIndex(interfaces, i);

            if (CFConvert.GetString(SystemConfiguration.SCNetworkInterfaceGetBSDName(networkInterface)) is { Length: > 0 } bsdName &&
                CFConvert.GetString(SystemConfiguration.SCNetworkInterfaceGetLocalizedDisplayName(networkInterface)) is { Length: > 0 } displayName) {

                names[bsdName] = displayName;
            }
        }

        return names;
    }

    public static string? GetPrimaryInterface()
    {
        using CFScope storeName = new(CFConvert.CreateString(DynamicStoreName));
        using CFScope store     = new(SystemConfiguration.SCDynamicStoreCreate(nint.Zero, storeName, nint.Zero, nint.Zero));

        if (store.IsNull) {
            return null;
        }

        foreach (string key in GlobalStateKeys) {
            using CFScope keyName = new(CFConvert.CreateString(key));
            using CFScope state   = new(SystemConfiguration.SCDynamicStoreCopyValue(store, keyName));

            if (!state.IsNull &&
                CFConvert.ToDictionary(state).TryGetValue(SystemConfiguration.PrimaryInterfaceKey, out nint primary) &&
                CFConvert.GetString(primary) is { Length: > 0 } name) {

                return name;
            }
        }

        return null;
    }
}
#endif
