#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Tests.Drivers;

// The kext interop lives with the Drivers tests, which read the loaded kexts through it.
public sealed class KextManagerTests
{
    [Fact]
    public void GetLoadedKextInfo_Includes_The_BSD_Kernel_Interface()
    {
        Dictionary<string, object?>? loaded = IOKit.GetLoadedKextInfo(["CFBundleVersion", "OSBundlePath"]);

        Assert.NotNull(loaded);

        Dictionary<string, object?> bsd = Assert.IsType<Dictionary<string, object?>>(loaded["com.apple.kpi.bsd"]);

        Assert.IsType<string>(bsd["CFBundleVersion"]);
        Assert.IsType<string>(bsd["OSBundlePath"]);
    }

    // The full entries carry class and dependency tables; only the asked-for keys (plus the two
    // KextManager always adds) should come back.
    [Fact]
    public void GetLoadedKextInfo_Reads_Only_The_Requested_Keys()
    {
        Dictionary<string, object?>? loaded = IOKit.GetLoadedKextInfo(["CFBundleVersion"]);

        Assert.NotNull(loaded);

        foreach (object? value in loaded.Values) {
            Dictionary<string, object?> entry = Assert.IsType<Dictionary<string, object?>>(value);

            Assert.DoesNotContain("OSBundleClasses", entry.Keys);
            Assert.DoesNotContain("OSBundleDependencies", entry.Keys);
        }
    }
}
#endif
