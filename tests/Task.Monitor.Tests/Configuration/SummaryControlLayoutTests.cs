using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Configuration;
using Task.Monitor.Tests.Common;
using Task.Monitor.Tests.Gui.Controls.Summary2;

namespace Task.Monitor.Tests.Configuration;

public sealed class SummaryControlLayoutTests
{
    [Fact]
    public void LayoutNode_Canary_Test() =>
        Assert.Equal(7, CanaryTestHelper.GetPropertyCount<LayoutNode<SummaryPane>>());

    // A new pane setting has to be written to and read from the layout file too.
    [Fact]
    public void SummaryPane_Canary_Test() =>
        Assert.Equal(4, CanaryTestHelper.GetPropertyCount<SummaryPane>());

    // A CpuCores pane is designer-only (no menu or screen builds one), so persistence is the only
    // thing that carries it between sessions.
    [Fact]
    public void CpuCores_Pane_Round_Trips()
    {
        LayoutTree<SummaryPane> original = LayoutTree<SummaryPane>.FromNodes(
            [new LayoutNode<SummaryPane> { Id = 0, Pane = new(PaneControlType.CpuCores) }], rootId: 0);

        string iniText = SummaryControlLayout.FromTree("Cores", original).ToString();

        ConfigParser parser = new(iniText);
        parser.Parse();

        LayoutTree<SummaryPane> tree = new SummaryControlLayout(parser.Sections[0]).ToTree();

        Assert.Equal(PaneControlType.CpuCores, Assert.Single(tree.Panes()).Pane.ControlType);
    }

    [Fact]
    public void FromTree_Then_ToTree_Round_Trips_The_Example_Tree()
    {
        LayoutTree<SummaryPane> original = TestLayouts.Example();
        SummaryControlLayout layout = SummaryControlLayout.FromTree("My Dashboard", original);

        LayoutTree<SummaryPane> roundTripped = layout.ToTree();

        Assert.Equal(original.RootId, roundTripped.RootId);
        Assert.Equal(original.Nodes.Count, roundTripped.Nodes.Count);

        foreach ((int id, LayoutNode<SummaryPane> expected) in original.Nodes) {
            LayoutNode<SummaryPane> actual = roundTripped.Nodes[id];

            Assert.Equal(expected.IsSplit, actual.IsSplit);
            Assert.Equal(expected.Orientation, actual.Orientation);
            Assert.Equal((double)expected.Ratio, actual.Ratio, precision: 5);
            Assert.Equal(expected.FirstId, actual.FirstId);
            Assert.Equal(expected.SecondId, actual.SecondId);
            Assert.Equal(expected.Pane, actual.Pane);
        }
    }

    // The version key is what lets a later taskmon tell this format from its own, so a saved
    // layout is written at the current version, as "version=2" for this format.
    [Fact]
    public void FromTree_Writes_The_Current_Version()
    {
        ConfigParser parser = new(SummaryControlLayout.FromTree("My Dashboard", TestLayouts.Example()).ToString());
        parser.Parse();

        Assert.Equal(2, SummaryControlLayout.CurrentVersion);
        Assert.Equal("2", parser.Sections[0].GetString(Constants.Keys.Version));
    }

    [Theory]
    [InlineData(PaneControlType.CpuMetre)]
    [InlineData(PaneControlType.GpuMetre)]
    [InlineData(PaneControlType.GpuMemoryMetre)]
    [InlineData(PaneControlType.MemoryMetre)]
    [InlineData(PaneControlType.VirtualMemoryMetre)]
    [InlineData(PaneControlType.DiskMetre)]
    [InlineData(PaneControlType.NetworkSendMetre)]
    [InlineData(PaneControlType.NetworkReceiveMetre)]
    public void Metre_Panes_Round_Trip(PaneControlType type)
    {
        LayoutTree<SummaryPane> original = LayoutTree<SummaryPane>.FromNodes(
            [new LayoutNode<SummaryPane> { Id = 0, Pane = new(type) }], rootId: 0);

        string iniText = SummaryControlLayout.FromTree("Metre", original).ToString();

        ConfigParser parser = new(iniText);
        parser.Parse();

        LayoutTree<SummaryPane> tree = new SummaryControlLayout(parser.Sections[0]).ToTree();

        Assert.Equal(type, Assert.Single(tree.Panes()).Pane.ControlType);
    }

    [Fact]
    public void Name_Reflects_The_Backing_Sections_Name()
    {
        SummaryControlLayout layout = SummaryControlLayout.FromTree("Custom Name", TestLayouts.Example());

        Assert.Equal("Custom Name", layout.Name);
    }

    [Theory]
    [InlineData(PaneControlType.Power)]
    [InlineData(PaneControlType.Health)]
    [InlineData(PaneControlType.ThermalList)]
    [InlineData(PaneControlType.GpuStack)]
    public void Power_Health_Thermal_List_And_Gpu_Stack_Panes_Round_Trip(PaneControlType controlType)
    {
        LayoutTree<SummaryPane> original = LayoutTree<SummaryPane>.FromNodes(
            [new LayoutNode<SummaryPane> { Id = 0, Pane = new(controlType) }], rootId: 0);

        string iniText = SummaryControlLayout.FromTree(controlType.ToString(), original).ToString();

        ConfigParser parser = new(iniText);
        parser.Parse();

        LayoutTree<SummaryPane> tree = new SummaryControlLayout(parser.Sections[0]).ToTree();

        Assert.Equal(controlType, Assert.Single(tree.Panes()).Pane.ControlType);
    }

    // A process pane's sort follows its columns: pane,Process,<columns>,<sort>,<Asc|Desc>. A sort
    // column taskmon doesn't know is dropped, leaving SummaryControl2's default.
    [Theory]
    [InlineData("pane,Process,Process+Pid+Mem,Gpu,Asc",  Statistics.Process | Statistics.Pid | Statistics.Mem, Statistics.Gpu,  true)]
    [InlineData("pane,Process,Process+Pid+Mem,Gpu,Desc", Statistics.Process | Statistics.Pid | Statistics.Mem, Statistics.Gpu,  false)]
    [InlineData("pane,process,,disk",                    null,                                                 Statistics.Disk, false)]
    [InlineData("pane,Process,Process+Pid,Bogus,asc",    Statistics.Process | Statistics.Pid,                  null,            true)]
    [InlineData("pane,Process,Process+Pid",              Statistics.Process | Statistics.Pid,                  null,            false)]
    public void Process_Pane_Reads_Its_Columns_And_Sort(string raw, Statistics? columns, Statistics? sort, bool ascending)
    {
        ConfigSection section = new ConfigSection("Sorted")
            .Add(Constants.Keys.Version, "2")
            .Add(Constants.Keys.SummaryNodes, "0")
            .Add($"{Constants.Keys.SummaryNodePrefix}0", raw);

        SummaryPane pane = Assert.Single(new SummaryControlLayout(section).ToTree().Panes()).Pane;

        Assert.Equal(new SummaryPane(PaneControlType.Process, columns, sort, ascending), pane);
    }

    [Theory]
    [InlineData(Statistics.Process | Statistics.Pid | Statistics.Mem, Statistics.Gpu, true,  "pane,Process,Process+Pid+Mem,Gpu,Asc")]
    [InlineData(null,                                                 Statistics.Gpu, false, "pane,Process,,Gpu,Desc")]
    [InlineData(Statistics.Process | Statistics.Pid | Statistics.Mem, null,           false, "pane,Process,Process+Pid+Mem")]
    [InlineData(null,                                                 null,           false, "pane,Process")]
    public void Process_Pane_Writes_Its_Columns_And_Sort(Statistics? columns, Statistics? sort, bool ascending, string expected)
    {
        SummaryPane original = new(PaneControlType.Process, columns, sort, ascending);
        LayoutTree<SummaryPane> tree = new(original);

        ConfigParser parser = new(SummaryControlLayout.FromTree("Sorted", tree).ToString());
        parser.Parse();

        Assert.Equal(expected, parser.Sections[0].GetString($"{Constants.Keys.SummaryNodePrefix}{tree.RootId}"));
        Assert.Equal(original, Assert.Single(new SummaryControlLayout(parser.Sections[0]).ToTree().Panes()).Pane);
    }

    // These were pane controls during v2 development; a layout saved then still loads, with an empty pane.
    [Theory]
    [InlineData("pane,Drivers")]
    [InlineData("pane,Services")]
    [InlineData("pane,Startup")]
    [InlineData("pane,InstalledApps")]
    [InlineData("pane,SystemInfo")]
    [InlineData("pane,Thermals")]
    public void Removed_Pane_Controls_Load_As_Empty_Panes(string raw)
    {
        ConfigSection section = new ConfigSection("Removed")
            .Add(Constants.Keys.Version, "2")
            .Add(Constants.Keys.SummaryNodes, "0")
            .Add($"{Constants.Keys.SummaryNodePrefix}0", raw);

        SummaryPane pane = Assert.Single(new SummaryControlLayout(section).ToTree().Panes()).Pane;

        Assert.Equal(PaneControlType.Empty, pane.ControlType);
    }

    // Confirms the round trip survives an actual reparse of the written text, not just the
    // in-memory ConfigSection - ';' is a comment marker to ConfigParser, so this is what proves
    // the "," / "+" separator choice (see the class comment) actually holds up.
    [Fact]
    public void ToString_Output_Reparses_To_An_Equivalent_Tree()
    {
        LayoutTree<SummaryPane> original = TestLayouts.Example();
        SummaryControlLayout layout = SummaryControlLayout.FromTree("My Dashboard", original);

        string iniText = layout.ToString();

        ConfigParser parser = new(iniText);
        parser.Parse();
        SummaryControlLayout reparsed = new(parser.Sections[0]);

        Assert.Equal("My Dashboard", reparsed.Name);

        LayoutTree<SummaryPane> tree = reparsed.ToTree();

        Assert.Equal(original.RootId, tree.RootId);
        Assert.Equal(original.Nodes.Count, tree.Nodes.Count);

        SummaryPane processPane = tree.Panes().Single(p => p.Pane.ControlType == PaneControlType.Process).Pane;
        Assert.Equal(
            Statistics.Process | Statistics.Pid | Statistics.Cpu | Statistics.Mem,
            processPane.ProcessColumns);
    }

    // Regression test: with no root= line the root used to come back as node 0 whatever the
    // file's node ids were - ConfigSection.GetInt ignored the first-node default - leaving a tree
    // rooted at a node that doesn't exist.
    [Fact]
    public void ToTree_Without_A_Root_Key_Roots_The_Tree_At_The_First_Node()
    {
        ConfigSection section = new ConfigSection("No Root")
            .Add(Constants.Keys.Version, "2")
            .Add(Constants.Keys.SummaryNodes, "3,4,5")
            .Add($"{Constants.Keys.SummaryNodePrefix}3", "split,Row,0.5,4,5")
            .Add($"{Constants.Keys.SummaryNodePrefix}4", "pane,Cpu")
            .Add($"{Constants.Keys.SummaryNodePrefix}5", "pane,Memory");

        LayoutTree<SummaryPane> tree = new SummaryControlLayout(section).ToTree();

        Assert.Equal(3, tree.RootId);
        Assert.Equal(
            [PaneControlType.Cpu, PaneControlType.Memory],
            tree.Panes().Select(pane => pane.Pane.ControlType).ToArray());
    }

    [Fact]
    public void ToTree_With_No_Backing_Section_Gives_The_Fallback_Layout()
    {
        SummaryControlLayout layout = new();

        LayoutTree<SummaryPane> tree = layout.ToTree();

        Assert.Equal(SummaryControlLayout.Fallback().Panes(), tree.Panes());
    }

    [Fact]
    public void ToTree_With_No_Node_Keys_Gives_The_Fallback_Layout()
    {
        ConfigSection section = new ConfigSection("Empty").Add(Constants.Keys.Version, "2");
        SummaryControlLayout layout = new(section);

        LayoutTree<SummaryPane> tree = layout.ToTree();

        Assert.Equal(SummaryControlLayout.Fallback().Panes(), tree.Panes());
    }
}
