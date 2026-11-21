#if __APPLE__
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Native;

namespace Task.Monitor.System.Tests.Native;

public sealed class SysctlTests
{
    [Fact]
    public unsafe void A_Failed_Read_Leaves_The_Caller_Nothing_To_Free()
    {
        // No such MIB: the caller must not be left holding a pointer Sysctl already freed.
        ReadOnlySpan<int> name = [(int)Sys.Selectors.CTL_VM, int.MaxValue];
        byte* buffer = null;
        int length = 0;

        Assert.False(Sysctl.TryRead(name, ref buffer, ref length));
        Assert.True(buffer == null);
    }

    [Fact]
    public unsafe void A_Successful_Read_Hands_The_Caller_A_Buffer()
    {
        ReadOnlySpan<int> name = [(int)Sys.Selectors.CTL_VM, Sys.VM_SWAPUSAGE];
        byte* buffer = null;
        int length = 0;

        Assert.True(Sysctl.TryRead(name, ref buffer, ref length));
        Assert.True(buffer != null);
        Assert.Equal(sizeof(Sys.XswUsage), length);

        Marshal.FreeHGlobal((nint)buffer);
    }

    [Fact]
    public void GetArgMax_Reports_The_Kernel_Limit() =>
        Assert.True(Sysctl.GetArgMax() > 0);

    [Fact]
    public void GetProcessExecPath_Has_Nothing_For_A_Process_That_Does_Not_Exist() =>
        Assert.Null(Sysctl.GetProcessExecPath(int.MaxValue, new byte[4096]));

    [Fact]
    public void GetProcessExecPath_Reads_This_Process()
    {
        byte[] buffer = new byte[Sysctl.GetArgMax() ?? 1024 * 1024];

        Assert.False(string.IsNullOrEmpty(Sysctl.GetProcessExecPath(Environment.ProcessId, buffer)));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 0, 0, 0 })]
    [InlineData(new byte[] { 1, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 1, 0, 0, 0, (byte)'a' })]
    public void ParseExecPath_Has_Nothing_Without_A_Terminated_Path(byte[] procArgs) =>
        Assert.Null(Sysctl.ParseExecPath(procArgs));

    [Fact]
    public void ParseExecPath_Reads_The_Path_After_Argc()
    {
        byte[] procArgs = [2, 0, 0, 0, .. "/bin/sleep\0\0\0sleep\0infinity\0"u8];

        Assert.Equal("/bin/sleep", Sysctl.ParseExecPath(procArgs));
    }
}
#endif
