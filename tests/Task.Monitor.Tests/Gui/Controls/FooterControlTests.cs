using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Services;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class FooterControlTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public FooterControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private static SystemSnapshot SnapshotWith(params ServiceHealth[] services) =>
        new() { Services = services };

    private FooterControl CreateControl(int width = 160)
    {
        FooterControl ctrl = new(
            runContext.ServiceController,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = width,
            Height = 1
        };

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        FooterControl ctrl = new(
            runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    [Fact]
    public void Draws_The_Version_Banner_Without_A_Snapshot()
    {
        FooterControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("Task Monitor v", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Draws_A_Token_For_Every_Service_In_Order()
    {
        FooterControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            new ServiceHealth("Cpu", ServiceStatus.Running),
            new ServiceHealth("Memory", ServiceStatus.Running),
            new ServiceHealth("Gpu", ServiceStatus.Starting),
            new ServiceHealth("Disk", ServiceStatus.Errored)));

        string output = CapturedOutput();

        int cpu = output.IndexOf("Cpu", StringComparison.Ordinal);
        int memory = output.IndexOf("Memory", StringComparison.Ordinal);
        int gpu = output.IndexOf("Gpu", StringComparison.Ordinal);
        int disk = output.IndexOf("Disk", StringComparison.Ordinal);

        Assert.True(cpu >= 0 && memory > cpu && gpu > memory && disk > gpu,
            $"token order was cpu={cpu} memory={memory} gpu={gpu} disk={disk}");

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Renders_A_Service_Differently_By_Status()
    {
        FooterControl running = CreateControl();
        running.Sample(SnapshotWith(new ServiceHealth("Disk", ServiceStatus.Running)));
        string runningOutput = CapturedOutput();
        running.Unload();

        runContextHelper.terminal.Invocations.Clear();

        FooterControl errored = CreateControl();
        errored.Sample(SnapshotWith(new ServiceHealth("Disk", ServiceStatus.Errored)));
        string erroredOutput = CapturedOutput();
        errored.Unload();

        Assert.NotEqual(runningOutput, erroredOutput);
    }

    [Fact]
    public void Redraws_Only_When_The_Health_List_Changes()
    {
        FooterControl ctrl = CreateControl();

        ServiceHealth[] health = [ new ServiceHealth("Cpu", ServiceStatus.Running) ];

        ctrl.Sample(SnapshotWith(health));
        runContextHelper.terminal.Invocations.Clear();

        ctrl.Sample(SnapshotWith(new ServiceHealth("Cpu", ServiceStatus.Running)));

        Assert.Empty(CapturedOutput());

        ctrl.Unload();
    }
}
