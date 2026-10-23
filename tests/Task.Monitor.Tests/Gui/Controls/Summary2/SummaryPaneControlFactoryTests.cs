using Task.Monitor.Gui.Controls.Cpu;
using Task.Monitor.Gui.Controls.Disk;
using Task.Monitor.Gui.Controls.Gpu;
using Task.Monitor.Gui.Controls.Memory;
using Task.Monitor.Gui.Controls.Network;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

public sealed class SummaryPaneControlFactoryTests
{
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
