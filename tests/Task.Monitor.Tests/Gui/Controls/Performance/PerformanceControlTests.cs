using System.Drawing;
using System.Reflection;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Npu;
using Task.Monitor.System.Services.Power;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls.Performance;

public sealed class PerformanceControlTests
{
    private static readonly Color HighlightColour = ColorTranslator.FromHtml("#3b5070");
    private static readonly Color StripColour     = ColorTranslator.FromHtml("#1a1b26");
    private static readonly Color TextColour      = ColorTranslator.FromHtml("#9ece6a");
    private static readonly Color PanelColour     = ColorTranslator.FromHtml("#0f1610");

    // At 140 wide the column is its widest, 42, and its text 28 beside the 14-wide chart.
    private const int EntryHeight = 7;
    private const int TextWidth   = 42 - 14;
    private const int TitleRow    = 1;

    private readonly RunContextHelper runContextHelper;
    private readonly RunContext       runContext;

    public PerformanceControlTests()
    {
        runContextHelper = new RunContextHelper();
        runContext       = runContextHelper.GetRunContext();
    }

    // The all-disks entry gives way to one per disk, and the selection moves to the first of them.
    [Fact]
    public void A_Selected_Entry_That_Goes_Passes_The_Selection_To_One_Of_Its_Kind()
    {
        PerformanceControl ctrl = Create();

        Press(ctrl, ConsoleKey.DownArrow, times: 3);
        RaiseSnapshotUpdated(SnapshotWithDisks(4, 5));

        Assert.Equal(4, Assert.IsType<DiskPerformanceControl>(ctrl.Selected).DiskIndex);

        ctrl.Unload();
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    private PerformanceControl Create(int width = 160, int height = 50, ISystemTerminal? terminal = null)
    {
        PerformanceControl ctrl = new(runContext.ServiceController, terminal ?? new ForwardingTerminal(runContext.Terminal), runContext.AppConfig) {
            Width  = width,
            Height = height
        };

        ctrl.Load();
        ctrl.Resize();

        return ctrl;
    }

    // Two entries: the CPU's, selected (without focus), over the memory's.
    private string DrawColumn(Color panelBg, Color stripBg)
    {
        RecordingTerminal terminal = new();
        Theme             theme    = runContext.AppConfig.Theme;

        theme.BackgroundHighlightInactive        = HighlightColour;
        theme.ForegroundHighlightInactive        = TextColour;
        theme.PerformancePanelBackground         = panelBg;
        theme.PerformancePanelForeground         = TextColour;
        theme.PerformancePanelTitlebarBackground = stripBg;
        theme.PerformancePanelTitlebarForeground = TextColour;

        PerformanceControl ctrl = Create(width: 140, height: 2 * EntryHeight, terminal);

        terminal.Reset();
        ctrl.Draw();
        ctrl.Unload();

        return terminal.Output;
    }

    [Fact]
    public void Draws_An_Npu_Entry_Titled_Like_The_Gpu()
    {
        PerformanceControl ctrl = Create();

        RaiseSnapshotUpdated(SnapshotWithNpu());

        string output = CapturedOutput();

        Assert.Contains("NPU 0  Apple Neural Engine", output);
        Assert.Contains("16 cores", output);
        Assert.Contains("(1.3 W)", output);

        ctrl.Unload();
    }

    // A detail is kept while its device is, so its charts keep their history.
    [Fact]
    public void Each_Disk_Gets_An_Entry_That_Lasts_While_The_Disk_Does()
    {
        PerformanceControl ctrl = Create();

        RaiseSnapshotUpdated(SnapshotWithDisks(0, 1));
        List<DiskPerformanceControl> first = [.. ctrl.Details.OfType<DiskPerformanceControl>()];

        RaiseSnapshotUpdated(SnapshotWithDisks(0, 1));
        List<DiskPerformanceControl> second = [.. ctrl.Details.OfType<DiskPerformanceControl>()];

        Assert.Equal([0, 1], first.Select(disk => disk.DiskIndex));
        Assert.Equal(first, second);

        ctrl.Unload();
    }

    [Fact]
    public void Each_Npu_Comes_After_The_Gpu()
    {
        PerformanceControl ctrl = Create();

        RaiseSnapshotUpdated(SnapshotWithNpu());

        Assert.Equal(
            [typeof(CpuPerformanceControl), typeof(MemoryPerformanceControl), typeof(GpuPerformanceControl), typeof(NpuPerformanceControl), typeof(DiskPerformanceControl), typeof(NetworkPerformanceControl)],
            Kinds(ctrl));

        ctrl.Unload();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Entry_Charts_Follow_ShowSmallMetreGrid_On_Every_Load(bool showGrid)
    {
        runContext.AppConfig.ShowSmallMetreGrid = !showGrid;
        runContext.AppConfig.ShowLargeMetreGrid = !showGrid;

        PerformanceControl ctrl = Create(width: 120, height: 40);
        ctrl.Unload();

        runContext.AppConfig.ShowSmallMetreGrid = showGrid;
        ctrl.Load();

        Assert.All(ctrl.Details, detail => Assert.Equal(showGrid, ((IPerformanceDetail)detail).Spark.ShowGrid));

        ctrl.Unload();
    }

    private static Type[] Kinds(PerformanceControl ctrl) => [.. ctrl.Details.Select(detail => detail.GetType())];

    private static void Press(PerformanceControl ctrl, ConsoleKey key, int times = 1)
    {
        for (int i = 0; i < times; i++) {
            bool handled = false;
            ctrl.KeyPressed(new ConsoleKeyInfo('\0', key, false, false, false), ref handled);
        }
    }

    // ServiceController only raises this from its worker loop.
    private void RaiseSnapshotUpdated(SystemSnapshot snapshot)
    {
        MulticastDelegate? handler = (MulticastDelegate?)typeof(ServiceController)
            .GetField("SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(runContext.ServiceController);

        handler?.DynamicInvoke(runContext.ServiceController, new SystemSnapshotEventArgs(snapshot));
    }

    // Without corner glyphs (conhost) the selected entry's highlight is a block of whole cells one cell
    // in from each edge. The title sits inside it, and the edge cells are blank panel background.
    [Fact]
    public void Selected_Entry_Without_Quadrants_Keeps_Its_Text_Inside_A_One_Cell_Margin()
    {
        Control.DrawingCapabilities = DrawingCapabilities.SeamlessBlockGlyphs;

        try {
            AnsiScreen screen = new(DrawColumn(PanelColour, StripColour));

            string panel     = AnsiScreen.Key(PanelColour);
            string highlight = AnsiScreen.Key(HighlightColour);

            for (int y = 0; y < EntryHeight; y++) {
                for (int x = 0; x < TextWidth; x++) {
                    bool edge = x == 0 || x == TextWidth - 1 || y == 0 || y == EntryHeight - 1;

                    Assert.Equal(edge ? panel : highlight, screen[x, y].Background);

                    if (edge) {
                        Assert.Equal(' ', screen[x, y].Glyph);
                    }
                }
            }

            Assert.Equal(new CpuPerformanceControl(runContext.Terminal, runContext.AppConfig).Title[0], screen[1, TitleRow].Glyph);
        }
        finally {
            Control.DrawingCapabilities = DrawingCapabilities.All;
        }
    }

    private static SystemSnapshot SnapshotWithDisks(params int[] indexes) =>
        new() {
            Disk = new DiskInfo {
                Specs   = new DiskSpecs { Devices = [.. indexes.Select(index => new DiskDevice { Index = index })] },
                Metrics = new DiskMetrics { Devices = [.. indexes.Select(index => new DiskDeviceMetrics { Index = index })] }
            }
        };

    internal static SystemSnapshot SnapshotWithNpu() =>
        new() {
            Npu = new NpuInfo {
                Specs   = new NpuSpecs { Devices = [new NpuDevice { Index = 0, Description = "Apple Neural Engine", Vendor = "Apple", Cores = 16, Architecture = "h15g", Version = "144.17" }] },
                Metrics = new NpuMetrics { Devices = [new NpuDeviceMetrics { Index = 0, ActiveRatio = 0.5 }] }
            },
            Power = new PowerInfo {
                Metrics = new PowerMetrics {
                    Readings = [new PowerReading { Component = PowerComponent.NeuralEngine, Rail = "Neural Engine", Watts = 1.3 }]
                }
            }
        };

    // Before any snapshot there is one entry for each kind, covering all its devices; an NPU only
    // gets one when there is a device.
    [Fact]
    public void Starts_With_One_Entry_Per_Kind_And_No_Npu()
    {
        PerformanceControl ctrl = Create();

        Assert.Equal(
            [typeof(CpuPerformanceControl), typeof(MemoryPerformanceControl), typeof(GpuPerformanceControl), typeof(DiskPerformanceControl), typeof(NetworkPerformanceControl)],
            Kinds(ctrl));

        ctrl.Unload();
    }

    // With room for three entries, the column scrolls just far enough to keep the selection in view.
    [Fact]
    public void The_Column_Scrolls_To_Keep_The_Selection_In_View()
    {
        PerformanceControl ctrl = Create(height: 3 * EntryHeight);

        bool[] Shown() => [.. ctrl.Details.Select(detail => ((IPerformanceDetail)detail).Spark.Visible)];

        Press(ctrl, ConsoleKey.DownArrow, times: 2);
        bool[] lastInView = Shown();

        Press(ctrl, ConsoleKey.DownArrow, times: 2);
        bool[] atTheEnd = Shown();

        Press(ctrl, ConsoleKey.UpArrow, times: 3);
        bool[] backAtTheSecond = Shown();

        ctrl.Unload();

        Assert.Equal([true, true, true, false, false], lastInView);
        Assert.Equal([false, false, true, true, true], atTheEnd);
        Assert.Equal([false, true, true, true, false], backAtTheSecond);
    }

    // Regression test: on themes with a transparent panel background (e.g. Tokyo Night) the
    // half-cell edges of an unselected entry's title strip drew a bar in the title text colour.
    // Each edge cell must be half panel background (outside) and half strip colour (inside).
    [Theory]
    [InlineData(true,  false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true,  true)]
    public void Unselected_Title_Edges_Are_Half_Panel_Background_And_Half_Strip(bool panelTransparent, bool stripTransparent)
    {
        Color panelBg = panelTransparent ? ConsolePalette.Transparent : PanelColour;
        Color stripBg = stripTransparent ? ConsolePalette.Transparent : StripColour;

        AnsiScreen screen = new(DrawColumn(panelBg, stripBg));

        string outside = AnsiScreen.Key(panelBg);
        string inside  = AnsiScreen.Key(stripBg);

        Assert.Equal((outside, inside), screen.HalvesAt(x: 0,             y: EntryHeight + TitleRow));
        Assert.Equal((inside, outside), screen.HalvesAt(x: TextWidth - 1, y: EntryHeight + TitleRow));
    }

    // Terminal.app leaves a seam above '▌' and '▐', so there the edges are whole strip cells.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Unselected_Title_Edges_Are_Whole_Strip_Cells_Without_Seamless_Glyphs(bool panelTransparent)
    {
        Color  panelBg = panelTransparent ? ConsolePalette.Transparent : PanelColour;
        string inside  = AnsiScreen.Key(StripColour);

        Control.DrawingCapabilities = DrawingCapabilities.QuadrantGlyphs;

        try {
            AnsiScreen screen = new(DrawColumn(panelBg, StripColour));

            Assert.Equal((inside, inside), screen.HalvesAt(x: 0,             y: EntryHeight + TitleRow));
            Assert.Equal((inside, inside), screen.HalvesAt(x: TextWidth - 1, y: EntryHeight + TitleRow));
        }
        finally {
            Control.DrawingCapabilities = DrawingCapabilities.All;
        }
    }
}
