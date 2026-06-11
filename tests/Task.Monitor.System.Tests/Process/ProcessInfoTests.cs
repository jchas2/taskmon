using System.Reflection;
using Task.Monitor.System.Process;
using Task.Monitor.Tests.Common;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Tests.Process;

public sealed class ProcessInfoTests
{
    [Fact]
    public void ProcessInfo_Canary_Test() =>
        Assert.Equal(20, CanaryTestHelper.GetPropertyCount<ProcessInfo>());

    [Fact]
    public void Should_Construct_ProcessInfo_From_Process()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        ProcessInfo? processInfo = new ProcessService().GetProcessById(currentProcess.Id);

        Assert.NotNull(processInfo);
        Assert.InRange(processInfo.Pid, 0, int.MaxValue);
        ProcessInfoHelpers.AssertProcessInfoProperties(processInfo);
        ProcessInfoHelpers.AssertProcessInfoProperties(currentProcess, processInfo);
    }
}
