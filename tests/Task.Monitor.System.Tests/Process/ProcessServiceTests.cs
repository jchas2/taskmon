using Task.Monitor.System.Process;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Tests.Process;

public sealed class ProcessServiceTests
{
    [Fact]
    public void Should_Return_Processes()
    {
        List<ProcessInfo> processInfos = new ProcessService().GetProcesses().ToList();
        Assert.InRange(processInfos.Count, 1, int.MaxValue);
    }
    
    [Fact]
    public void Should_Get_Process_Properties_For_Current_Process() 
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        ProcessInfo? processInfo = new ProcessService().GetProcessById(currentProcess.Id);

        Assert.NotNull(processInfo);
        ProcessInfoHelpers.AssertProcessInfoProperties(processInfo);
        ProcessInfoHelpers.AssertProcessInfoProperties(currentProcess, processInfo);
    }

    [Fact]
    public void Should_Return_ProcessInfo_By_Id()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        ProcessInfo? processInfo = new ProcessService().GetProcessById(currentProcess.Id);
        
        Assert.NotNull(processInfo);
        ProcessInfoHelpers.AssertProcessInfoProperties(processInfo);
        ProcessInfoHelpers.AssertProcessInfoProperties(currentProcess, processInfo);
    }
}