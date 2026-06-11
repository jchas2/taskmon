using System.Runtime.Versioning;
using Task.Monitor.System.Process;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Tests.Process;

public sealed class ThreadServiceTests
{
    [Fact]
    public void Should_Return_Threads_For_Current_Process()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        List<ThreadInfo> threads = new ThreadService().GetThreads(currentProcess.Id);
        Assert.True(threads.Count > 0);
    }

    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Threads_In_Wait_State_Should_Have_Reason()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        
        List<ThreadInfo> threads = new ThreadService()
            .GetThreads(currentProcess.Id)
            .Where(t => t.ThreadState.Equals("Wait", StringComparison.CurrentCultureIgnoreCase)).ToList();

        if (threads.Count > 0) {
            Assert.Equal(0, threads.Count(wt => string.IsNullOrEmpty(wt.Reason)));
        }
    }
}
