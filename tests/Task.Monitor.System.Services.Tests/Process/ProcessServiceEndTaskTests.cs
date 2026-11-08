using System.Runtime.Versioning;
using Task.Monitor.System.Services.Process;
using Task.Monitor.Tests.Common;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Services.Tests.Process;

public partial class ProcessServiceEndTaskTests
{
    [SkippableFact]
    [SupportedOSPlatform("macos")]
    public void Should_End_MacOS_Task()
    {
        // TODO: Debug on MacOS.
        // using FileCleanupHelper cleanupHelper = new();
        // string fileName = cleanupHelper.GetTempFile(".sh");
        // Should_End_Task(fileName);
    }

    private void Should_End_Task(string fileName)
    {
        CreateScriptFile(fileName);

        using SysDiag::Process? process = SysDiag::Process.Start(
            new SysDiag::ProcessStartInfo {
                UseShellExecute = true,
                FileName = fileName
            });

        Assert.NotNull(process);
        Thread.Sleep(millisecondsTimeout: 200);

        bool terminated = new ProcessService().EndTask(process.Id, timeoutMilliseconds: 500);
        Assert.True(terminated);
    }

    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Should_End_Windows_Task()
    {
        using FileCleanupHelper cleanupHelper = new();
        string fileName = cleanupHelper.GetTempFile(".bat");
        Should_End_Task(fileName);
    }

    [Fact]
    public void Should_Get_Process_By_Pid()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        bool result = ProcessService.TryGetProcessByPid(currentProcess.Id, out SysDiag::Process? process);

        Assert.True(result);
        Assert.NotNull(process);

        process.Dispose();
    }

    [Fact]
    public void Should_Not_End_A_Task_For_A_Negative_Pid() =>
        Assert.False(new ProcessService().EndTask(-1, timeoutMilliseconds: 0));

    [Fact]
    public void Should_Not_Get_Process_For_A_Negative_Pid()
    {
        bool result = ProcessService.TryGetProcessByPid(-1, out SysDiag::Process? process);

        Assert.False(result);
        Assert.Null(process);
    }
}
