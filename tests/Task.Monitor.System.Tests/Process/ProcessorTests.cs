using System.Diagnostics;
using Xunit.Abstractions;
using TaskMgrProcess = Task.Monitor.System.Process;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Tests.Process;

public sealed class ProcessorTests
{
    private readonly ITestOutputHelper testOutputHelper;
    
    public ProcessorTests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void Should_Return_ProcessInfos_In_Minimum_Time()
    {
        const int numberOfIterations = 10;
        //const int maxTimeTakenInMilliseconds = 100;

        //var processor = new TaskMgrProcess::Processor();

        for (int i = 0; i < numberOfIterations; i++) {
            //var timeTaken = Time(() => processor.GetAll());
            //Assert.True(timeTaken.Milliseconds < maxTimeTakenInMilliseconds);
            //_testOutputHelper.WriteLine($"ms: {timeTaken.Milliseconds}");
        }
    }

    [Fact]
    public void Should_Run_All_ProcessInfos_And_Verify_Current_Process()
    {
        // using var currentProcess = SysDiag::Process.GetCurrentProcess();
        //
        // var processor = new TaskMgrProcess::Processor();
        // processor.Run();
        //
        // TaskMgrProcess::ProcessInfo[] procInfos = [];
        // int runAway = 10;
        // int iteration = 0;
        //
        // while (procInfos.Length == 0 && iteration++ < runAway) {
        //     Thread.Sleep(TaskMgrProcess::Processor.UpdateTimeInMs);
        //     //procInfos = processor.GetAll();
        // }
        //
        // Assert.True(iteration < runAway);
        //
        // var currentProcInfo = procInfos.Single(p => p.Pid == currentProcess.Id);
        // Assert.Equal(currentProcInfo.ExeName, currentProcess.ProcessName);
        //
        // processor.Stop();
        
        // TODO: Need a property on Processor that confirms thread has terminated.
    }

    private TimeSpan Time(Action toTime)
    {
        var timer = Stopwatch.StartNew();
        toTime();
        timer.Stop();
        return timer.Elapsed;
    }
}
