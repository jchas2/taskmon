#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class SysctlTests
{
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
    public void GetArgMax_Reports_The_Kernel_Limit() =>
        Assert.True(Sys.GetArgMax() > 0);

    [Fact]
    public void GetProcessExecPath_Has_Nothing_For_A_Process_That_Does_Not_Exist() =>
        Assert.Null(Sys.GetProcessExecPath(int.MaxValue, new byte[4096]));

    [Fact]
    public void GetProcessExecPath_Reads_This_Process()
    {
        byte[] buffer = new byte[Sys.GetArgMax() ?? 1024 * 1024];

        Assert.False(string.IsNullOrEmpty(Sys.GetProcessExecPath(Environment.ProcessId, buffer)));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 0, 0, 0 })]
    [InlineData(new byte[] { 1, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 1, 0, 0, 0, (byte)'a' })]
    public void ParseExecPath_Has_Nothing_Without_A_Terminated_Path(byte[] procArgs) =>
        Assert.Null(Sys.ParseExecPath(procArgs));

    [Fact]
    public void ParseExecPath_Reads_The_Path_After_Argc()
    {
        byte[] procArgs = [2, 0, 0, 0, .. "/bin/sleep\0\0\0sleep\0infinity\0"u8];

        Assert.Equal("/bin/sleep", Sys.ParseExecPath(procArgs));
    }
}
#endif
