using Task.Monitor.Gui.Controls.Cpu;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.Metre;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class CpuMetreControlTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public CpuMetreControlTests()
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

    [Fact]
    public void Configures_A_Bordered_Single_Row_Dots_Metre_Without_A_Legend()
    {
        CpuMetreControl ctrl = CreateControl();
        MetreControl metre = ctrl.Metre;

        Assert.True(metre.Border);
        Assert.Equal(MetreControlStyle.Dots, metre.MetreStyle);
        Assert.False(metre.ShowLegend);
        Assert.Equal(1, metre.Rows);
        Assert.Equal(3, metre.RequiredHeight);
        Assert.Equal(MetreTextPosition.TopLeft, metre.TextPosition);
        Assert.Equal("CPU%", metre.Text);
        Assert.Equal(["Kernel", "User"], metre.Series.Select(series => series.Label));

        ctrl.Unload();
    }

    private CpuMetreControl CreateControl(int width = 40, int height = 3)
    {
        CpuMetreControl ctrl = new(
            runContext.ServiceController,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width  = width,
            Height = height
        };

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    [Fact]
    public void Ignores_A_Snapshot_With_No_Cpu_Info()
    {
        CpuMetreControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());

        Assert.Equal("CPU%", ctrl.Metre.Text);
        Assert.All(ctrl.Metre.Series, series => Assert.Equal(0.0, series.Value));

        ctrl.Unload();
    }

    [Fact]
    public void Reloading_Does_Not_Duplicate_The_Series()
    {
        CpuMetreControl ctrl = CreateControl();

        ctrl.Unload();
        ctrl.Load();

        Assert.Equal(2, ctrl.Metre.SeriesCount);

        ctrl.Unload();
    }

    [Fact]
    public void Sample_Shows_Kernel_And_User_Time_And_The_Total_In_The_Border()
    {
        CpuMetreControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot {
            Cpu = new CpuInfo { Metrics = new CpuMetrics { CpuPercentKernelTime = 0.05, CpuPercentUserTime = 0.085 } }
        });

        Assert.Equal(0.05,  ctrl.Metre.Series[0].Value);
        Assert.Equal(0.085, ctrl.Metre.Series[1].Value);
        Assert.Equal("CPU% 13.5%", ctrl.Metre.Text);
        Assert.Contains(" CPU% 13.5% ", CapturedOutput());

        ctrl.Unload();
    }
}
