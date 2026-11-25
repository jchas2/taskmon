using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Summary2;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

// The outline panes a layout preview is drawn with: what each pane becomes, and how an outline draws.
public sealed class SummaryOutlineTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext       runContext;

    public SummaryOutlineTests()
    {
        runContextHelper = new RunContextHelper();
        runContext       = runContextHelper.GetRunContext();
    }

    [Theory]
    [MemberData(nameof(PaneTypes))]
    public void An_Outline_Keeps_The_Fixed_Height_Of_The_Pane_It_Stands_For(PaneControlType type)
    {
        LayoutTree<SummaryPane> tree = new(new SummaryPane(type));

        SummaryControl2 real    = new(runContext.ServiceController, Terminal(), runContext.AppConfig, tree);
        SummaryControl2 outline = new(runContext.ServiceController, Terminal(), runContext.AppConfig, tree, showOutlines: true);

        Assert.Equal(real.FixedHeight(tree.RootId), outline.FixedHeight(tree.RootId));
    }

    [Fact]
    public void A_Box_Too_Small_To_Frame_Is_Shaded()
    {
        new EmptyPaneControl(Terminal()) { Width = 3, Height = 5, Label = "Cpu" }.Draw();

        Assert.Contains("░░░", Written());
        Assert.DoesNotContain("╭", Written());
    }

    [Fact]
    public void A_Chart_Outline_Draws_A_Line_Along_Its_Bottom_Row()
    {
        new EmptyPaneControl(Terminal()) { Width = 20, Height = 6, Label = "Cpu", Hint = PaneHint.Chart }.Draw();

        Assert.Contains("⣀⣀⣠⣤⣴⣶⣾⣿", Written());
    }

    [Fact]
    public void A_Detail_Line_Sits_Under_The_Label()
    {
        new EmptyPaneControl(Terminal()) { Width = 30, Height = 7, Label = "Process", Detail = "CPU %, Memory" }.Draw();

        string written = Written();

        Assert.True(written.IndexOf("Process", StringComparison.Ordinal) < written.IndexOf("CPU %, Memory", StringComparison.Ordinal));
    }

    // Too short for its rows, a Health pane is hidden, and its space must not keep what was drawn there before.
    [Fact]
    public void A_Hidden_Fixed_Pane_Clears_Its_Space()
    {
        SummaryControl2 summary = Summary(new LayoutTree<SummaryPane>(new SummaryPane(PaneControlType.Health)), width: 30, height: 5);

        summary.Draw();

        Assert.False(summary.PaneControls.Values.Single().Visible);
        Assert.Contains(new string(' ', 30), Written());
    }

    [Fact]
    public void A_Label_Too_Long_For_Its_Box_Ends_With_An_Ellipsis()
    {
        new EmptyPaneControl(Terminal()) { Width = 8, Height = 3, Label = "Network Received" }.Draw();

        Assert.Contains("Netwo…", Written());
    }

    [Fact]
    public void A_List_Outline_Draws_Rules_Below_Its_Label()
    {
        new EmptyPaneControl(Terminal()) { Width = 40, Height = 10, Label = "Process", Hint = PaneHint.List }.Draw();

        Assert.Contains("────────── ──────", Written());
    }

    [Fact]
    public void A_Metre_Outline_Draws_Its_Bar_Beside_The_Label()
    {
        new EmptyPaneControl(Terminal()) { Width = 30, Height = 3, Label = "Cpu Metre", Hint = PaneHint.Metre }.Draw();

        Assert.Contains("Cpu Metre ⣿", Written());
    }

    // The sort column is marked with its direction, and is listed even when the columns leave it out.
    [Theory]
    [InlineData(Statistics.Process | Statistics.Pid | Statistics.Cpu | Statistics.Mem, null, false, "CPU % ↓, Memory")]
    [InlineData(null, null, false, "CPU % ↓, Memory")]
    [InlineData(Statistics.Process | Statistics.Pid | Statistics.Mem, Statistics.Gpu, true, "GPU % ↑, Memory")]
    public void A_Process_Outline_Lists_Its_Own_Columns_And_Sort(Statistics? columns, Statistics? sort, bool ascending, string detail) =>
        Assert.Equal(detail, Outline(new SummaryPane(PaneControlType.Process, columns, sort, ascending)).Detail);

    private EmptyPaneControl Outline(SummaryPane pane) =>
        Assert.IsType<EmptyPaneControl>(Summary(new LayoutTree<SummaryPane>(pane)).PaneControls.Values.Single());

    [Theory]
    [InlineData(PaneControlType.Empty,    "empty")]
    [InlineData(PaneControlType.Cpu,      "Cpu")]
    [InlineData(PaneControlType.CpuCores, "Cpu Cores")]
    [InlineData(PaneControlType.Health,   "Health")]
    public void Outlines_Are_Labelled_With_The_Pane_Name(PaneControlType type, string label) =>
        Assert.Equal(label, Outline(new SummaryPane(type)).Label);

    [Fact]
    public void Outlines_Take_The_Theme_Colours_Of_Their_Kind()
    {
        LayoutTree<SummaryPane> tree = new(new SummaryPane(PaneControlType.Cpu));
        tree.Split(tree.RootId, Orientation.Row, new SummaryPane(PaneControlType.Process));

        SummaryControl2 summary = Summary(tree);
        summary.Load();

        Theme                  theme    = runContext.AppConfig.Theme;
        List<EmptyPaneControl> outlines = summary.PaneControls.Values.OfType<EmptyPaneControl>().ToList();

        EmptyPaneControl chart = outlines.Single(outline => outline.Hint == PaneHint.Chart);
        EmptyPaneControl list  = outlines.Single(outline => outline.Hint == PaneHint.List);

        Assert.Equal(theme.ChartBorderForeground,    chart.BorderColour);
        Assert.Equal(theme.RangeLowBackground,       chart.HintColour);
        Assert.Equal(theme.ListViewBorderForeground, list.BorderColour);
        Assert.Equal(theme.ListViewBackground,       list.BackgroundColour);

        summary.Unload();
    }

    [Theory]
    [InlineData(PaneControlType.Empty,     PaneHint.None)]
    [InlineData(PaneControlType.Cpu,       PaneHint.Chart)]
    [InlineData(PaneControlType.CpuMetre,  PaneHint.Metre)]
    [InlineData(PaneControlType.CpuCores,  PaneHint.Metre)]
    [InlineData(PaneControlType.Process,   PaneHint.List)]
    [InlineData(PaneControlType.Health,    PaneHint.List)]
    public void Outline_Hints_Follow_The_Kind_Of_Pane(PaneControlType type, PaneHint hint) =>
        Assert.Equal(hint, Outline(new SummaryPane(type)).Hint);

    [Theory]
    [InlineData(PaneControlType.Fans,     "Fans")]
    [InlineData(PaneControlType.Npu,      "Npu")]
    [InlineData(PaneControlType.NpuMetre, "Npu Metre")]
    public void Panes_Only_On_MacOS_Say_So_Elsewhere(PaneControlType type, string name)
    {
        EmptyPaneControl outline = Outline(new SummaryPane(type));

#if __APPLE__
        Assert.Equal(name, outline.Label);
        Assert.NotEqual(PaneHint.None, outline.Hint);
#else
        Assert.Equal($"{name} (macOS only)", outline.Label);
        Assert.Equal(PaneHint.None, outline.Hint);
#endif
    }

    public static IEnumerable<object[]> PaneTypes() =>
        Enum.GetValues<PaneControlType>().Select(type => new object[] { type });

    private SummaryControl2 Summary(LayoutTree<SummaryPane> tree, int width = 60, int height = 20)
    {
        SummaryControl2 summary = new(runContext.ServiceController, Terminal(), runContext.AppConfig, tree, showOutlines: true) {
            Width  = width,
            Height = height
        };

        summary.Resize();

        return summary;
    }

    // Spans reach the mock as strings through ForwardingTerminal, so every write can be read back.
    private ForwardingTerminal Terminal() => new(runContext.Terminal);

    private string Written() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write")
            .Select(invocation => invocation.Arguments switch {
                [string text]           => text,
                [char glyph]            => glyph.ToString(),
                [char glyph, int count] => new string(glyph, count),
                                      _ => string.Empty
            }));
}
