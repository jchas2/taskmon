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
        Assert.Equal(2, CanaryTestHelper.GetPropertyCount<SummaryPane>());

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

    [Fact]
    public void IsTreeLayout_Is_False_For_An_Unrecognised_LayoutType_Value()
    {
        ConfigSection section = new ConfigSection("Something Else").Add("layout-type", "grid");
        SummaryControlLayout layout = new(section);

        Assert.False(layout.IsTreeLayout);
    }

    [Fact]
    public void IsTreeLayout_Is_False_For_A_Section_With_No_LayoutType_Key()
    {
        ConfigSection gridSection = new ConfigSection("All Charts")
            .Add("ratio", "0.6")
            .Add("num-rows", "2")
            .Add("num-cols", "4");

        SummaryControlLayout layout = new(gridSection);

        Assert.False(layout.IsTreeLayout);
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

    [Fact]
    public void Power_Pane_Round_Trips()
    {
        LayoutTree<SummaryPane> original = LayoutTree<SummaryPane>.FromNodes(
            [new LayoutNode<SummaryPane> { Id = 0, Pane = new(PaneControlType.Power) }], rootId: 0);

        string iniText = SummaryControlLayout.FromTree("Power", original).ToString();

        ConfigParser parser = new(iniText);
        parser.Parse();

        LayoutTree<SummaryPane> tree = new SummaryControlLayout(parser.Sections[0]).ToTree();

        Assert.Equal(PaneControlType.Power, Assert.Single(tree.Panes()).Pane.ControlType);
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
        Assert.True(reparsed.IsTreeLayout);

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
            .Add(Constants.Keys.LayoutType, "tree")
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
        ConfigSection section = new ConfigSection("Empty").Add("layout-type", "tree");
        SummaryControlLayout layout = new(section);

        LayoutTree<SummaryPane> tree = layout.ToTree();

        Assert.Equal(SummaryControlLayout.Fallback().Panes(), tree.Panes());
    }
}
