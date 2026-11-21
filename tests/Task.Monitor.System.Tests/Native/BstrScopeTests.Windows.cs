#if __WIN32__
using System.Runtime.InteropServices;
using Task.Monitor.System.Native;

namespace Task.Monitor.System.Tests.Native;

public sealed class BstrScopeTests
{
    [Fact]
    public void Allocate_Holds_The_String()
    {
        using BstrScope scope = BstrScope.Allocate("SELECT * FROM Win32_Processor");

        Assert.False(scope.IsNull);
        Assert.Equal("SELECT * FROM Win32_Processor", Marshal.PtrToStringBSTR(scope));
    }

    [Fact]
    public void Dispose_Ignores_A_Null_Bstr()
    {
        using BstrScope scope = new(nint.Zero);

        Assert.True(scope.IsNull);
    }
}
#endif
