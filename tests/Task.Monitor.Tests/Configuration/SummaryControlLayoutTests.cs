using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Configuration;

namespace Task.Monitor.Tests.Configuration;

public sealed class SummaryControlLayoutTests
{
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

        SummaryLayoutTree tree = new SummaryControlLayout(section).ToTree();

        Assert.Equal(3, tree.RootId);
        Assert.Equal(
            [PaneControlType.Cpu, PaneControlType.Memory],
            tree.Panes().Select(pane => pane.ControlType).ToArray());
    }

    [Fact]
    public void FromTree_Then_ToTree_Round_Trips_The_Example_Tree()
    {
        SummaryLayoutTree original = SummaryLayoutTree.CreateExample();
        SummaryControlLayout layout = SummaryControlLayout.FromTree("My Dashboard", original);

        SummaryLayoutTree roundTripped = layout.ToTree();

        Assert.Equal(original.RootId, roundTripped.RootId);
        Assert.Equal(original.Nodes.Count, roundTripped.Nodes.Count);

        foreach ((int id, SummaryLayoutNode expected) in original.Nodes) {
            SummaryLayoutNode actual = roundTripped.Nodes[id];

            Assert.Equal(expected.IsSplit, actual.IsSplit);
            Assert.Equal(expected.Orientation, actual.Orientation);
            Assert.Equal((double)expected.Ratio, actual.Ratio, precision: 5);
            Assert.Equal(expected.FirstId, actual.FirstId);
            Assert.Equal(expected.SecondId, actual.SecondId);
            Assert.Equal(expected.ControlType, actual.ControlType);
            Assert.Equal(expected.ProcessColumns, actual.ProcessColumns);
        }
    }

    // A CpuCores pane is designer-only (no menu or screen builds one), so persistence is the only
    // thing that carries it between sessions.
    [Fact]
    public void CpuCores_Pane_Round_Trips()
    {
        SummaryLayoutTree original = SummaryLayoutTree.FromNodes(
            [new SummaryLayoutNode { Id = 0, ControlType = PaneControlType.CpuCores }], rootId: 0);

        string iniText = SummaryControlLayout.FromTree("Cores", original).ToString();

        ConfigParser parser = new(iniText);
        parser.Parse();

        SummaryLayoutTree tree = new SummaryControlLayout(parser.Sections[0]).ToTree();

        Assert.Equal(PaneControlType.CpuCores, Assert.Single(tree.Panes()).ControlType);
    }

    // Confirms the round trip survives an actual reparse of the written text, not just the
    // in-memory ConfigSection - ';' is a comment marker to ConfigParser, so this is what proves
    // the "," / "+" separator choice (see the class comment) actually holds up.
    [Fact]
    public void ToString_Output_Reparses_To_An_Equivalent_Tree()
    {
        SummaryLayoutTree original = SummaryLayoutTree.CreateExample();
        SummaryControlLayout layout = SummaryControlLayout.FromTree("My Dashboard", original);

        string iniText = layout.ToString();

        ConfigParser parser = new(iniText);
        parser.Parse();
        SummaryControlLayout reparsed = new(parser.Sections[0]);

        Assert.Equal("My Dashboard", reparsed.Name);
        Assert.True(reparsed.IsTreeLayout);

        SummaryLayoutTree tree = reparsed.ToTree();

        Assert.Equal(original.RootId, tree.RootId);
        Assert.Equal(original.Nodes.Count, tree.Nodes.Count);

        SummaryLayoutNode processPane = tree.Panes().Single(p => p.ControlType == PaneControlType.Process);
        Assert.Equal(
            Statistics.Process | Statistics.Pid | Statistics.Cpu | Statistics.Mem,
            processPane.ProcessColumns);
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

    [Fact]
    public void IsTreeLayout_Is_False_For_An_Unrecognised_LayoutType_Value()
    {
        ConfigSection section = new ConfigSection("Something Else").Add("layout-type", "grid");
        SummaryControlLayout layout = new(section);

        Assert.False(layout.IsTreeLayout);
    }

    [Fact]
    public void ToTree_With_No_Backing_Section_Falls_Back_To_The_Example_Tree()
    {
        SummaryControlLayout layout = new();

        SummaryLayoutTree tree = layout.ToTree();

        Assert.Equal(SummaryLayoutTree.CreateExample().Nodes.Count, tree.Nodes.Count);
    }

    [Fact]
    public void ToTree_With_No_Node_Keys_Falls_Back_To_The_Example_Tree()
    {
        ConfigSection section = new ConfigSection("Empty").Add("layout-type", "tree");
        SummaryControlLayout layout = new(section);

        SummaryLayoutTree tree = layout.ToTree();

        Assert.Equal(SummaryLayoutTree.CreateExample().Nodes.Count, tree.Nodes.Count);
    }

    [Fact]
    public void Name_Reflects_The_Backing_Sections_Name()
    {
        SummaryControlLayout layout = SummaryControlLayout.FromTree("Custom Name", SummaryLayoutTree.CreateExample());

        Assert.Equal("Custom Name", layout.Name);
    }
}
