using Task.Monitor.Gui.Controls.Cpu;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class CpuKernelUserMetreControlTests
{
    private readonly RunContext runContext = new RunContextHelper().GetRunContext();

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        CpuKernelUserMetreControl ctrl = new(runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private CpuKernelUserMetreControl CreateControl(RecordingTerminal terminal)
    {
        CpuKernelUserMetreControl ctrl = new(terminal, runContext.AppConfig) {
            Width  = 60,
            Height = CpuKernelUserMetreControl.MetreHeight
        };

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    // SUMMARY hides a pane that is too short for its fixed height, and the metre draws itself on
    // every sample, so hiding the control has to hide the metre as well.
    [Fact]
    public void Hiding_The_Control_Hides_Its_Metre()
    {
        RecordingTerminal terminal = new();
        CpuKernelUserMetreControl ctrl = CreateControl(terminal);

        ctrl.Visible = false;
        terminal.Reset();
        ctrl.Sample(new CpuMetrics { CpuPercentKernelTime = 0.05, CpuPercentUserTime = 0.085 });

        Assert.False(ctrl.Metre.Visible);
        Assert.Equal(string.Empty, terminal.Output);

        ctrl.Unload();
    }

    [Fact]
    public void Keeps_Its_Two_Series_Across_Reloads()
    {
        CpuKernelUserMetreControl ctrl = CreateControl(new RecordingTerminal());

        ctrl.Unload();
        ctrl.Load();

        Assert.Equal(2, ctrl.Metre.Series.Count);

        ctrl.Unload();
    }

    [Fact]
    public void RequiredHeight_Is_Three_Rows_A_Legend_And_A_Border()
    {
        CpuKernelUserMetreControl ctrl = CreateControl(new RecordingTerminal());

        Assert.Equal(6, ctrl.RequiredHeight);
        Assert.Equal(CpuKernelUserMetreControl.MetreHeight, ctrl.RequiredHeight);

        ctrl.Unload();
    }

    [Fact]
    public void Sample_Shows_Kernel_And_User_Time_In_Their_Colours_And_The_Legend()
    {
        RecordingTerminal terminal = new();
        CpuKernelUserMetreControl ctrl = CreateControl(terminal);

        ctrl.Sample(new CpuMetrics { CpuPercentKernelTime = 0.05, CpuPercentUserTime = 0.085 });

        Assert.Equal(0.05,  ctrl.Metre.Series[0].Value);
        Assert.Equal(0.085, ctrl.Metre.Series[1].Value);
        Assert.Equal(runContext.AppConfig.Theme.PerformanceCpuKernel, ctrl.Metre.Series[0].Colour);
        Assert.Equal(runContext.AppConfig.Theme.PerformanceCpuUser,   ctrl.Metre.Series[1].Colour);

        string output = AnsiTestHelper.StripColours(terminal.Output);

        Assert.Contains("Kernel 5.0%", output);
        Assert.Contains("User 8.5%", output);

        ctrl.Unload();
    }
}
