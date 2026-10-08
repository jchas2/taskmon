using System.Runtime.InteropServices;

namespace Task.Monitor.Cli.Utils.Tests;

public sealed class HGlobalScopeTests
{
    [Fact]
    public void Allocate_Returns_A_Usable_Buffer()
    {
        using HGlobalScope scope = HGlobalScope.Allocate(sizeof(int));

        Assert.False(scope.IsNull);

        Marshal.WriteInt32(scope, 0x1234_5678);
        Assert.Equal(0x1234_5678, Marshal.ReadInt32(scope.Value));
    }

    [Fact]
    public void Adopts_An_Existing_Allocation()
    {
        nint value = Marshal.AllocHGlobal(16);

        using HGlobalScope scope = new(value);

        nint converted = scope;
        Assert.Equal(value, converted);
    }

    [Fact]
    public void Disposing_A_Null_Scope_Does_Nothing()
    {
        using HGlobalScope scope = new(nint.Zero);

        Assert.True(scope.IsNull);
    }
}
