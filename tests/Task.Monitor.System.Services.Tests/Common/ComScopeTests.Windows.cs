#if __WIN32__
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Tests.Common;

public sealed class ComScopeTests
{
    private const uint CLSCTX_INPROC_SERVER = 1;

    private static readonly Guid CLSID_ShellLink = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid IID_IUnknown    = new("00000000-0000-0000-C000-000000000046");

    [Fact]
    public void Dispose_Ignores_A_Null_Pointer()
    {
        using ComScope scope = new(nint.Zero);

        Assert.True(scope.IsNull);
    }

    [Fact]
    public void Dispose_Releases_One_Reference()
    {
        using ComInitScope com = ComInitScope.Initialise(Ole32.COINIT_MULTITHREADED);

        Assert.True(Ole32.CoCreateInstance(
            in CLSID_ShellLink,
            nint.Zero,
            CLSCTX_INPROC_SERVER,
            in IID_IUnknown,
            out nint shellLink) >= 0);

        // The test keeps its own reference, so the object outlives the scope.
        Assert.Equal(2, Marshal.AddRef(shellLink));

        using (ComScope scope = new(shellLink)) {
            nint value = scope;
            Assert.Equal(shellLink, value);
        }

        Assert.Equal(0, Marshal.Release(shellLink));
    }
}
#endif
