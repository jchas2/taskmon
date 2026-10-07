#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class CFScopeTests
{
    // A CFString long and unusual enough not to be one of CF's shared constant strings, whose
    // retain counts are meaningless.
    private static nint CreateString() => CoreFoundation.CFStringCreate($"cfscope-test-{Guid.NewGuid():N}");

    [Fact]
    public void Releases_Its_Reference_Exactly_Once()
    {
        nint value = CreateString();
        CoreFoundation.CFRetain(value); // The test's own reference, so the object outlives the scope.

        try {
            Assert.Equal(2, CoreFoundation.CFGetRetainCount(value));

            using (CFScope scope = new(value)) {
                Assert.Equal(value, scope.Value);
            }

            Assert.Equal(1, CoreFoundation.CFGetRetainCount(value));
        }
        finally {
            CoreFoundation.CFRelease(value);
        }
    }

    [Fact]
    public void Disposing_A_Null_Scope_Does_Nothing()
    {
        using CFScope scope = new(nint.Zero);

        Assert.True(scope.IsNull);
    }

    [Fact]
    public void Converts_Implicitly_To_The_Reference()
    {
        using CFScope scope = new(CreateString());

        nint value = scope;

        Assert.Equal(scope.Value, value);
        Assert.False(scope.IsNull);
        Assert.StartsWith("cfscope-test-", CoreFoundation.GetString(scope));
    }
}
#endif
