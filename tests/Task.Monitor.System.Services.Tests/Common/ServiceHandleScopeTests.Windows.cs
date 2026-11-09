#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Tests.Common;

public sealed class ServiceHandleScopeTests
{
    [Fact]
    public void Dispose_Ignores_A_Null_Handle()
    {
        using ServiceHandleScope scope = new(nint.Zero);

        Assert.True(scope.IsNull);
    }

    [Fact]
    public void Value_Converts_Implicitly_To_The_Handle()
    {
        using ServiceHandleScope scope = new(WinService.OpenSCManager(
            lpMachineName:   null!,
            lpDatabaseName:  null!,
            dwDesiredAccess: WinService.SC_MANAGER_CONNECT));

        nint value = scope;

        Assert.False(scope.IsNull);
        Assert.Equal(scope.Value, value);
    }
}
#endif
