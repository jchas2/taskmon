#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class IOObjectScopeTests
{
    // Present on every Mac: the root of the device tree.
    private static uint GetPlatformExpert() =>
        IOKit.IOServiceGetMatchingService(0, IOKit.IOServiceMatching("IOPlatformExpertDevice"));

    [Fact]
    public void Releases_Its_Object_Exactly_Once()
    {
        uint service = GetPlatformExpert();
        Assert.NotEqual(0u, service);

        try {
            IOKit.IOObjectRetain(service); // Taken by the scope below.
            uint before = IOKit.IOObjectGetUserRetainCount(service);

            using (IOObjectScope scope = new(service)) {
                Assert.Equal(service, scope.Value);
            }

            Assert.Equal(before - 1, IOKit.IOObjectGetUserRetainCount(service));
        }
        finally {
            IOKit.IOObjectRelease(service);
        }
    }

    [Fact]
    public void Disposing_A_Null_Scope_Does_Nothing()
    {
        using IOObjectScope fromUint = new(0u);
        using IOObjectScope fromNint = new(nint.Zero);

        Assert.True(fromUint.IsNull);
        Assert.True(fromNint.IsNull);
    }

    [Fact]
    public void Converts_To_Both_Handle_Types()
    {
        using IOObjectScope scope = new(GetPlatformExpert());

        uint asUint = scope;
        nint asNint = scope;

        Assert.False(scope.IsNull);
        Assert.Equal(scope.Value, asUint);
        Assert.Equal((nint)scope.Value, asNint);
    }
}
#endif
