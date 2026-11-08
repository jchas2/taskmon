using System.Runtime.Versioning;
using Task.Monitor.System.Services.Process;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed class ProcessServiceThreadsTests
{
    [Fact]
    public void Should_Return_No_Threads_For_A_Negative_Pid() =>
        Assert.Empty(new ProcessService().GetThreads(-1));

    [Fact]
    public void Should_Return_Threads_For_Current_Process()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        List<ThreadInfo> threads = new ProcessService().GetThreads(currentProcess.Id);
        Assert.True(threads.Count > 0);
    }

    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Threads_In_Wait_State_Should_Have_Reason()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();

        List<ThreadInfo> threads = new ProcessService()
            .GetThreads(currentProcess.Id)
            .Where(t => t.ThreadState.Equals("Wait", StringComparison.CurrentCultureIgnoreCase)).ToList();

        if (threads.Count > 0) {
            Assert.Equal(0, threads.Count(wt => string.IsNullOrEmpty(wt.Reason)));
        }
    }
}
