using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Task.Monitor.Tests.Gui.Controls.Performance;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class NpuPerformanceControlTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public NpuPerformanceControlTests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    private NpuPerformanceControl CreateControl()
    {
        NpuPerformanceControl ctrl = new(new ForwardingTerminal(runContext.Terminal), runContext.AppConfig) {
            Width  = 120,
            Height = 40
        };

        ctrl.SetScope(0);
        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    [Fact]
    public void Draws_Both_Charts_The_Readings_And_The_Specs()
    {
        NpuPerformanceControl ctrl = CreateControl();

        ctrl.Sample(PerformanceControlNpuTests.SnapshotWithNpu() with {
            Cpu = new CpuInfo { Specs = new CpuSpecs { CpuName = "Apple M3 Pro" } }
        });
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("NPU 0  Apple Neural Engine", output);
        Assert.Contains("Power 1.3 W", output);
        Assert.Contains("Peak Power", output);
        Assert.Contains("Apple M3 Pro", output);
        Assert.Contains("h15g", output);
        Assert.Contains("144.17", output);

        ctrl.Unload();
    }

    [Fact]
    public void Draws_Nothing_Until_An_Npu_Is_Reported()
    {
        NpuPerformanceControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());
        ctrl.Draw();

        Assert.DoesNotContain("NPU", CapturedOutput());

        ctrl.Unload();
    }

    [Theory]
    [InlineData(null, 1.3, 1.3)]
    [InlineData(2.0,  1.3, 2.0)]
    [InlineData(1.0,  1.3, 1.3)]
    [InlineData(1.0,  null, 1.0)]
    public void Peak_Keeps_The_Highest_Power_Seen(double? peak, double? watts, double expected)
    {
        Assert.Equal(expected, NpuPerformanceControl.Peak(peak, watts));
    }
}
