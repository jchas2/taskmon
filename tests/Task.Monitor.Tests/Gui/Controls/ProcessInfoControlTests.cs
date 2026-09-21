using Moq;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Process;
using Task.Monitor.Tests.Common;
using Task.Monitor.Tests.Process;
using Xunit.Abstractions;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ProcessInfoControlTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    private readonly ProcessServiceFake processServiceFake = new();
    private readonly ModuleServiceFake moduleServiceFake = new();
    private readonly ThreadServiceFake threadServiceFake = new();

    public ProcessInfoControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        ProcessInfoControl ctrl = new(
            processServiceFake,
            moduleServiceFake,
            threadServiceFake,
            runContext.Terminal, 
            runContext.AppConfig);

        Assert.NotNull(ctrl);
    }
    
    [Fact]
    public void Constructor_With_Null_Terminal_Throws_ArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => 
            new ProcessInfoControl(
                processServiceFake,
                moduleServiceFake,
                threadServiceFake,
                null!, 
                runContext.AppConfig));

    // DETAIL is the first menu item and the tab shown by default (no selection needed).
    [Fact]
    public void Should_Draw_Detail_By_Default()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        ProcessInfo? processInfo = new ProcessService().GetProcessById(currentProcess.Id);

        Assert.NotNull(processInfo);

        processServiceFake.AddProcessInfo(processInfo);

        ProcessInfoControl ctrl = new(
            processServiceFake,
            moduleServiceFake,
            threadServiceFake,
            runContext.Terminal,
            runContext.AppConfig) {
            Width = 128,
            Height = 32
        };

        ctrl.AutoRefresh = false;
        ctrl.SelectedProcessId = currentProcess.Id;

        ctrl.Load();
        ctrl.Resize();
        ctrl.Draw();

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Pid:"))), Times.Once);
        // No verification for Pid.
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("File:"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("dotnet") || s.Contains("testhost.exe"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Description:"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Path:"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("dotnet") || s.Contains("testhost.exe"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("User:"))), Times.Once);
        // No verification for User.
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Version:"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Size:"))), Times.Once);
        // No verification for Size.
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("SELECT"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("DETAIL"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("THREADS"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("MODULES"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("HANDLES"))), Times.Once);

        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    // THREADS is menu index 1 (DETAIL=0, THREADS=1, MODULES=2, HANDLES=3) - its content only
    // draws once selected, since it's no longer the default active tab.
    [Fact]
    public void Should_Draw_Threads_When_Selected()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        ProcessInfo? processInfo = new ProcessService().GetProcessById(currentProcess.Id);

        Assert.NotNull(processInfo);

        processServiceFake.AddProcessInfo(processInfo);

        threadServiceFake.Add(
            new ThreadInfo {
                CpuKernelTime = new TimeSpan(hours: 0, minutes: 2, seconds: 7),
                CpuUserTime = new TimeSpan(hours: 0, minutes: 9, seconds: 41),
                CpuTotalTime = new TimeSpan(hours: 0, minutes: 11, seconds: 48),
                Priority = 8,
                Reason = string.Empty,
                StartAddress = 0x0,
                ThreadId = 1868067040,
                ThreadState = "Running"
            });

        // Wider than the DETAIL test: the THREADS columns' fixed widths sum to more than 128, so
        // a narrower control here would clip USER TIME/TOTAL TIME before this ever gets to assert
        // their content.
        ProcessInfoControl ctrl = new(
            processServiceFake,
            moduleServiceFake,
            threadServiceFake,
            runContext.Terminal,
            runContext.AppConfig) {
            Width = 160,
            Height = 32
        };

        ctrl.AutoRefresh = false;
        ctrl.SelectedProcessId = currentProcess.Id;

        ctrl.Load();
        ctrl.Resize();
        ctrl.SelectMenuItemForTests(1);

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("THREAD ID"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("STATE"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("REASON"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("PRI"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("START ADDRESS"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("KERNEL TIME"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("USER TIME"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("1868067040"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Running"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("8"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("0x0000000000000000"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("00:02:07"))), Times.Once);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("00:09:41"))), Times.Once);

        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    private ProcessInfoControl CreateLoadedControl()
    {
        ProcessInfoControl ctrl = new(
            processServiceFake,
            moduleServiceFake,
            threadServiceFake,
            runContext.Terminal,
            runContext.AppConfig) {
            Width = 128,
            Height = 32
        };

        ctrl.AutoRefresh = false;
        ctrl.Load();
        ctrl.Resize();

        return ctrl;
    }

    // ProcessInfo's setters are internal to Task.Monitor.System, so these use the real current
    // process (as the tests above do) rather than hand-built fixtures.
    private (int Pid, string ProcessName) AddCurrentProcess()
    {
        using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
        ProcessInfo? processInfo = new ProcessService().GetProcessById(currentProcess.Id);

        Assert.NotNull(processInfo);
        Assert.False(string.IsNullOrWhiteSpace(processInfo.ProcessName));

        processServiceFake.AddProcessInfo(processInfo);

        return (processInfo.Pid, processInfo.ProcessName);
    }

    [Fact]
    public void LoadProcess_Shows_Process_Name_In_Top_Border()
    {
        (int pid, string processName) = AddCurrentProcess();

        ProcessInfoControl ctrl = CreateLoadedControl();
        ctrl.LoadProcess(pid);

        Assert.Equal($"{processName} ({pid})", ctrl.ProcessTitle);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains($" {processName} ({pid}) "))), Times.AtLeastOnce);
    }

    [Fact]
    public void LoadProcess_Clears_Title_When_Process_Not_Found()
    {
        (int pid, _) = AddCurrentProcess();

        ProcessInfoControl ctrl = CreateLoadedControl();
        ctrl.LoadProcess(pid);
        ctrl.LoadProcess(-2);

        Assert.Equal(string.Empty, ctrl.ProcessTitle);
    }

    [Fact]
    public void ResetToDetail_Prevents_Module_Reload_On_Pid_Change()
    {
        (int pid, _) = AddCurrentProcess();

        ProcessInfoControl ctrl = CreateLoadedControl();
        ctrl.LoadProcess(pid);
        ctrl.SelectMenuItemForTests(2);

        int callsAfterModulesShown = moduleServiceFake.GetModulesCallCount;
        Assert.True(callsAfterModulesShown > 0);

        ctrl.ResetToDetail();
        ctrl.LoadProcess(-2);

        Assert.True(ctrl.IsDetailActiveForTests);
        Assert.Equal(0, ctrl.SelectedMenuIndexForTests);
        Assert.Equal(callsAfterModulesShown, moduleServiceFake.GetModulesCallCount);
    }

    [Fact]
    public void Process_Title_Persists_Across_Tab_Switch()
    {
        (int pid, string processName) = AddCurrentProcess();

        ProcessInfoControl ctrl = CreateLoadedControl();
        ctrl.LoadProcess(pid);
        runContextHelper.terminal.Invocations.Clear();

        ctrl.SelectMenuItemForTests(1);

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains($" {processName} ({pid}) "))), Times.AtLeastOnce);
    }
}
