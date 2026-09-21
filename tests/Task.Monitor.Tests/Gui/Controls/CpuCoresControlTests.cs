using System.Collections.Immutable;
using Task.Monitor.Gui.Controls.Cpu;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class CpuCoresControlTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public CpuCoresControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private CpuCoresControl CreateControl(int width, int height)
    {
        CpuCoresControl ctrl = new(runContext.ServiceController, runContext.Terminal, runContext.AppConfig) {
            Width = width,
            Height = height
        };

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    private static SystemSnapshot SnapshotWithCores(params double[] values) =>
        new() {
            Cpu = new CpuInfo {
                CoreMetrics = values
                    .Select((value, index) => new CpuInfo.CpuCoreMetric(index.ToString(), value))
                    .ToImmutableArray()
            }
        };

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Constructor_With_Null_Terminal_Throws_ArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            new CpuCoresControl(runContext.ServiceController, null!, runContext.AppConfig));

    // The worked example from the design: 8 cores in a 5-high pane fill the first column top to
    // bottom (C0-C4) before starting a second one (C5-C7).
    [Fact]
    public void Eight_Cores_In_Five_Rows_Fill_Two_Columns_Column_Major()
    {
        CpuCoresControl.CoreGrid grid = CpuCoresControl.CalculateGrid(
            coreCount: 8, innerWidth: 40, innerHeight: 5);

        Assert.Equal(2, grid.Columns);
        Assert.Equal(5, grid.Rows);
    }

    [Fact]
    public void Draws_Title_And_Every_Core_That_Fits()
    {
        CpuCoresControl ctrl = CreateControl(width: 42, height: 7);

        ctrl.Sample(SnapshotWithCores(0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8));

        string output = CapturedOutput();

        Assert.Contains("CPU CORES", output);

        for (int core = 0; core < 8; core++) {
            Assert.Contains($"C{core}", output);
        }

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Draws_Each_Cores_Percentage()
    {
        CpuCoresControl ctrl = CreateControl(width: 42, height: 6);

        ctrl.Sample(SnapshotWithCores(0.0, 0.5, 1.0));

        string output = CapturedOutput();

        Assert.Contains("0%", output);
        Assert.Contains("50%", output);
        Assert.Contains("100%", output);

        // The default metre style is Dots, so a fully loaded core's bar is drawn in full braille
        // cells - proof the metres themselves rendered, not just the labels around them.
        Assert.Contains('⣿', output);

        ctrl.Unload();
    }

    // Too narrow for two columns, so the cores that would have been in the second one are dropped
    // rather than drawn outside the pane.
    [Fact]
    public void Narrow_Control_Falls_Back_To_A_Single_Column()
    {
        CpuCoresControl.CoreGrid grid = CpuCoresControl.CalculateGrid(
            coreCount: 8, innerWidth: 18, innerHeight: 4);

        Assert.Equal(1, grid.Columns);
        Assert.Equal(4, grid.Rows);
        Assert.True(grid.BarWidth >= 6);
        Assert.True(grid.LabelWidth + 1 + grid.BarWidth + 4 <= 18);
    }

    // Whatever the pane size, the last column's rightmost cell (label + bar + percentage) has to
    // land inside the inner area, or a row would overwrite the right-hand border.
    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(64)]
    public void Columns_Always_Fit_Inside_The_Inner_Width(int coreCount)
    {
        for (int innerWidth = 1; innerWidth <= 120; innerWidth++) {
            for (int innerHeight = 1; innerHeight <= 12; innerHeight++) {
                CpuCoresControl.CoreGrid grid = CpuCoresControl.CalculateGrid(coreCount, innerWidth, innerHeight);

                if (grid.Columns == 0) {
                    continue;
                }

                const int columnGutter = 2;
                const int percentWidth = 4;

                int lastColumnLeft = (grid.Columns - 1) * (grid.ColumnWidth + columnGutter);
                int rowEnd = lastColumnLeft + grid.LabelWidth + 1 + grid.BarWidth + percentWidth;

                Assert.True(
                    rowEnd <= innerWidth,
                    $"{coreCount} cores at {innerWidth}x{innerHeight}: row ends at {rowEnd}, inner width {innerWidth}");
                Assert.True(grid.BarWidth >= 6);
                Assert.True(grid.Rows <= innerHeight);
            }
        }
    }

    [Fact]
    public void Too_Small_For_Any_Core_Draws_No_Columns()
    {
        CpuCoresControl.CoreGrid grid = CpuCoresControl.CalculateGrid(
            coreCount: 4, innerWidth: 6, innerHeight: 3);

        Assert.Equal(0, grid.Columns);
    }

    [Fact]
    public void Tiny_Control_Draws_Without_Throwing()
    {
        CpuCoresControl ctrl = CreateControl(width: 8, height: 3);

        ctrl.Sample(SnapshotWithCores(0.5, 0.6, 0.7, 0.8));
        ctrl.Draw();

        ctrl.Unload();
    }

    [Fact]
    public void No_Core_Data_Draws_The_Unavailable_Message()
    {
        CpuCoresControl ctrl = CreateControl(width: 60, height: 6);

        ctrl.Sample(new SystemSnapshot());

        string output = CapturedOutput();

        Assert.Contains("CPU CORES", output);
        Assert.Contains("not available", output);

        ctrl.Unload();
    }

    // The only route to this control: assigning PaneControlType.CpuCores to a pane. It takes no
    // input, so unlike the list panes it must stay out of the tab order.
    [Fact]
    public void Factory_Builds_The_Control_For_A_CpuCores_Pane()
    {
        Control control = SummaryPaneControlFactory.Create(
            new SummaryLayoutNode { Id = 0, ControlType = PaneControlType.CpuCores },
            runContext.ServiceController,
            runContext.Terminal,
            runContext.AppConfig);

        Assert.IsType<CpuCoresControl>(control);
        Assert.False(control.TabStop);
    }

    // A tick carrying fewer cores than the last one must not leave the dropped rows on screen.
    [Fact]
    public void Fewer_Cores_Than_Before_Clears_The_Stale_Rows()
    {
        CpuCoresControl ctrl = CreateControl(width: 42, height: 7);

        ctrl.Sample(SnapshotWithCores(0.1, 0.2, 0.3, 0.4));
        runContextHelper.terminal.Invocations.Clear();

        ctrl.Sample(SnapshotWithCores(0.1, 0.2));

        string output = CapturedOutput();

        Assert.Contains("C0", output);
        Assert.Contains("C1", output);
        Assert.DoesNotContain("C3", output);

        ctrl.Unload();
    }
}
