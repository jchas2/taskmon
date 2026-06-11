using System.Runtime.Versioning;
using Task.Monitor.System.Process;
using Task.Monitor.Tests.Common;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Tests.Process;

public partial class ProcessUtilsTests
{
    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Should_End_Windows_Task()
    {
        using FileCleanupHelper cleanupHelper = new();
        string fileName = cleanupHelper.GetTempFile(".bat");
        Should_End_Task(fileName);
    }

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

        bool terminated = ProcessUtils.EndTask(process.Id, timeOutMilliseconds: 500);
        Assert.True(terminated);
    } 
    
    [Fact]
    public void Should_Get_Process_By_Pid()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        bool result = ProcessUtils.TryGetProcessByPid(currentProcess.Id, out SysDiag::Process? process);
        
        Assert.True(result);
        Assert.NotNull(process);
        
        process.Dispose();
    }
}
