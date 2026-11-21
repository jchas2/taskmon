#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Native;

namespace Task.Monitor.System.Tests.Native;

public sealed class CFScopeTests
{
    [Fact]
    public void Converts_Implicitly_To_The_Reference()
    {
        using CFScope scope = new(CreateString());

        nint value = scope;

        Assert.Equal(scope.Value, value);
        Assert.False(scope.IsNull);
        Assert.StartsWith("cfscope-test-", CFConvert.GetString(scope));
    }

    // A CFString long and unusual enough not to be one of CF's shared constant strings, whose
    // retain counts are meaningless.
    private static nint CreateString() => CFConvert.CreateString($"cfscope-test-{Guid.NewGuid():N}");

    [Fact]
    public void Disposing_A_Null_Scope_Does_Nothing()
    {
        using CFScope scope = new(nint.Zero);

        Assert.True(scope.IsNull);
    }

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
}
#endif
