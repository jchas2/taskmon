#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class SysctlTests
{
    [Fact]
    public unsafe void A_Successful_Read_Hands_The_Caller_A_Buffer()
    {
        ReadOnlySpan<int> name = [(int)Sys.Selectors.CTL_VM, Sys.VM_SWAPUSAGE];
        byte* buffer = null;
        int length = 0;

        Assert.True(Sys.Sysctl(name, ref buffer, ref length));
        Assert.True(buffer != null);
        Assert.Equal(sizeof(Sys.XswUsage), length);

        Sys.FreeMemory(buffer);
    }

    [Fact]
    public unsafe void A_Failed_Read_Leaves_The_Caller_Nothing_To_Free()
    {
        // No such MIB: the caller must not be left holding a pointer Sysctl already freed.
        ReadOnlySpan<int> name = [(int)Sys.Selectors.CTL_VM, int.MaxValue];
        byte* buffer = null;
        int length = 0;

        Assert.False(Sys.Sysctl(name, ref buffer, ref length));
        Assert.True(buffer == null);
    }
}
#endif
