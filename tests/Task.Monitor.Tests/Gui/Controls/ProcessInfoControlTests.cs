using Moq;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.System.Process;
using Task.Monitor.Tests.Common;
using Task.Monitor.Tests.Process;
using Xunit.Abstractions;
using SysDiag = System.Diagnostics;
using WorkerTask = System.Threading.Tasks.Task;

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
    public void Constructor_With_Null_Terminal_Throws_ArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => 
            new ProcessInfoControl(
                processServiceFake,
                moduleServiceFake,
                threadServiceFake,
                null!, 
                runContext.AppConfig));

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

    // A refresh interval starts the thread refresh worker; without one there is no worker.
    private ProcessInfoControl CreateLoadedControl(TimeSpan? refreshInterval = null)
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

        ctrl.AutoRefresh = refreshInterval != null;
        ctrl.ThreadRefreshInterval = refreshInterval ?? ctrl.ThreadRefreshInterval;
        ctrl.Load();
        ctrl.Resize();

        return ctrl;
    }

    [Fact]
    public void LoadProcess_After_No_Selection_Loads_The_Process()
    {
        (int pid, string processName) = AddCurrentProcess();

        ProcessInfoControl ctrl = CreateLoadedControl();
        ctrl.LoadProcess(pid);

        Assert.Equal([pid], processServiceFake.RequestedPids);
        Assert.Equal([pid], threadServiceFake.RequestedPids);
        Assert.Equal($"{processName} ({pid})", ctrl.ProcessTitle);
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
    public void LoadProcess_Restores_The_Load_Errors_Once_A_Process_Is_Selected()
    {
        (int pid, _) = AddCurrentProcess();

        ProcessInfoControl ctrl = CreateLoadedControl();
        ctrl.LoadProcess(pid);

        // The fakes return no threads or modules, so a selected process shows the load errors.
        ctrl.SelectMenuItemForTests(1);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Threads could not be loaded"))), Times.AtLeastOnce);

        ctrl.SelectMenuItemForTests(2);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Modules could not be loaded"))), Times.AtLeastOnce);
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
    public void Load_Without_A_Selected_Process_Asks_No_Service_About_It()
    {
        ProcessInfoControl ctrl = CreateLoadedControl();
        ctrl.SelectMenuItemForTests(1);
        ctrl.SelectMenuItemForTests(2);

        // PROCESSES opens before ProcessControl has selected a row. Asking for pid -1 threw and
        // logged an exception on every open.
        Assert.Empty(processServiceFake.RequestedPids);
        Assert.Empty(threadServiceFake.RequestedPids);
        Assert.Empty(moduleServiceFake.RequestedPids);
        Assert.Equal(string.Empty, ctrl.ProcessTitle);
    }

    [Fact]
    public void Load_Without_A_Selected_Process_Shows_No_Load_Errors()
    {
        ProcessInfoControl ctrl = CreateLoadedControl();
        ctrl.SelectMenuItemForTests(1);
        ctrl.SelectMenuItemForTests(2);

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Threads could not be loaded"))), Times.Never);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Modules could not be loaded"))), Times.Never);
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

    [Fact]
    public async WorkerTask Reloading_Starts_A_New_Worker_While_The_Old_One_Exits()
    {
        ProcessInfoControl ctrl = CreateLoadedControl(refreshInterval: TimeSpan.FromMilliseconds(10));
        WorkerTask first = ctrl.WorkerTaskForTests!;

        ctrl.Unload();
        ctrl.Load();
        WorkerTask second = ctrl.WorkerTaskForTests!;

        Assert.NotSame(first, second);
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(second.IsCompleted);

        ctrl.Unload();

        await second.WaitAsync(TimeSpan.FromSeconds(5));
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

        ctrl.ResetToDetailPane();
        ctrl.LoadProcess(-2);

        Assert.True(ctrl.IsDetailActiveForTests);
        Assert.Equal(0, ctrl.SelectedMenuIndexForTests);
        Assert.Equal(callsAfterModulesShown, moduleServiceFake.GetModulesCallCount);
    }

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

    [Fact]
    public async WorkerTask The_Worker_Stops_Refreshing_Threads_Once_Unloaded()
    {
        (int pid, _) = AddCurrentProcess();

        ProcessInfoControl ctrl = CreateLoadedControl(refreshInterval: TimeSpan.FromMilliseconds(10));
        ctrl.LoadProcess(pid);
        ctrl.SelectMenuItemForTests(1);
        WorkerTask worker = ctrl.WorkerTaskForTests!;

        // LoadProcess asks once; anything more is the worker refreshing the THREADS tab.
        Assert.True(SpinWait.SpinUntil(() => ThreadRequestCount() > 1, TimeSpan.FromSeconds(5)));

        ctrl.Unload();
        int requestsAtUnload = ThreadRequestCount();

        await worker.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(requestsAtUnload, ThreadRequestCount());
    }

    // The worker asks the thread service while holding DrawingLock.
    private int ThreadRequestCount()
    {
        lock (ProcessInfoControl.DrawingLockForTests) {
            return threadServiceFake.RequestedPids.Count;
        }
    }

    [Fact]
    public async WorkerTask Unload_Does_Not_Wait_Out_The_Refresh_Interval()
    {
        ProcessInfoControl ctrl = CreateLoadedControl(refreshInterval: TimeSpan.FromSeconds(30));
        WorkerTask worker = ctrl.WorkerTaskForTests!;

        SysDiag::Stopwatch stopwatch = SysDiag::Stopwatch.StartNew();
        ctrl.Unload();
        stopwatch.Stop();

        // Unload used to wait for the worker to sleep out its interval, which held the next key
        // for up to a second after leaving PROCESSES. The cancelled wait now ends at once too.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        await worker.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async WorkerTask Unload_Inside_The_Drawing_Lock_Does_Not_Deadlock_With_The_Worker()
    {
        (int pid, _) = AddCurrentProcess();

        ProcessInfoControl ctrl = CreateLoadedControl(refreshInterval: TimeSpan.FromMilliseconds(10));
        ctrl.LoadProcess(pid);
        ctrl.SelectMenuItemForTests(1);
        WorkerTask worker = ctrl.WorkerTaskForTests!;

        // A menu key unloads PROCESSES from inside KeyPressed, which holds DrawingLock. Holding it
        // past a refresh interval leaves the worker blocked on the lock, as a refresh falling due
        // mid-key would. Waiting for the worker from inside the lock never returned.
        WorkerTask unload = WorkerTask.Run(() => {
            lock (ProcessInfoControl.DrawingLockForTests) {
                Thread.Sleep(100);
                ctrl.Unload();
            }
        });

        await unload.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
