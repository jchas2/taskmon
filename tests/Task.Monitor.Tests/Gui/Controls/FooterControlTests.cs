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
}
