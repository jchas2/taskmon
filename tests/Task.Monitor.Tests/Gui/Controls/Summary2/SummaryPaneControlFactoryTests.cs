using Task.Monitor.Gui.Controls.Cpu;
using Task.Monitor.Gui.Controls.Disk;
using Task.Monitor.Gui.Controls.Gpu;
using Task.Monitor.Gui.Controls.Memory;
using Task.Monitor.Gui.Controls.Network;
using Task.Monitor.Gui.Controls.Npu;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.Gui.Controls.Thermals;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

public sealed class SummaryPaneControlFactoryTests
{
    [Fact]
    public void Create_Builds_An_Npu_Chart_On_MacOS_Only()
    {
        RunContext runContext = new RunContextHelper().GetRunContext();

        Control control = SummaryPaneControlFactory.Create(
            new SummaryLayoutNode { Id = 0, ControlType = PaneControlType.Npu },
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig);

#if __APPLE__
        Chart chart = Assert.IsType<Chart>(control);
        Assert.Equal("Npu", chart.Text);
        Assert.False(chart.AutoScale);
#else
        Assert.IsType<EmptyPaneControl>(control);
#endif
    }

    [Fact]
    public void Create_Builds_An_Npu_Metre_On_MacOS_Only()
    {
        RunContext runContext = new RunContextHelper().GetRunContext();

        Control control = SummaryPaneControlFactory.Create(
            new SummaryLayoutNode { Id = 0, ControlType = PaneControlType.NpuMetre },
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig);

#if __APPLE__
        Assert.IsType<NpuMetreControl>(control);
        Assert.False(control.TabStop);
#else
        Assert.IsType<EmptyPaneControl>(control);
#endif
    }

    [Fact]
    public void Create_Builds_A_Fans_Pane_On_MacOS_Only()
    {
        RunContext runContext = new RunContextHelper().GetRunContext();

        Control control = SummaryPaneControlFactory.Create(
            new SummaryLayoutNode { Id = 0, ControlType = PaneControlType.Fans },
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig);

#if __APPLE__
        Assert.IsType<FansControl>(control);
        Assert.False(control.TabStop);
#else
        Assert.IsType<EmptyPaneControl>(control);
#endif
    }

    [Theory]
    [InlineData(PaneControlType.CpuMetre,            typeof(CpuMetreControl))]
    [InlineData(PaneControlType.GpuMetre,            typeof(GpuMetreControl))]
    [InlineData(PaneControlType.GpuMemoryMetre,      typeof(GpuMemoryMetreControl))]
    [InlineData(PaneControlType.MemoryMetre,         typeof(MemoryMetreControl))]
    [InlineData(PaneControlType.VirtualMemoryMetre,  typeof(VirtualMemoryMetreControl))]
    [InlineData(PaneControlType.DiskMetre,           typeof(DiskMetreControl))]
    [InlineData(PaneControlType.NetworkSendMetre,    typeof(NetworkSendMetreControl))]
    [InlineData(PaneControlType.NetworkReceiveMetre, typeof(NetworkReceiveMetreControl))]
    public void Create_Builds_The_Metre_For_Each_Metre_Pane(PaneControlType type, Type expected)
    {
        RunContext runContext = new RunContextHelper().GetRunContext();

        Control control = SummaryPaneControlFactory.Create(
            new SummaryLayoutNode { Id = 0, ControlType = type },
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig);

        Assert.IsType(expected, control);
        Assert.False(control.TabStop);
    }
}
