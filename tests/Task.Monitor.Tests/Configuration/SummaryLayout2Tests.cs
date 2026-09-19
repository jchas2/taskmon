using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Configuration;

namespace Task.Monitor.Tests.Configuration;

public sealed class SummaryLayout2Tests
{
    [Fact]
    public void FromTree_Then_ToTree_Round_Trips_The_Example_Tree()
    {
        SummaryLayoutTree original = SummaryLayoutTree.CreateExample();
        SummaryLayout2 layout = SummaryLayout2.FromTree("My Dashboard", original);

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

    // Confirms the round trip survives an actual reparse of the written text, not just the
    // in-memory ConfigSection - ';' is a comment marker to ConfigParser, so this is what proves
    // the "," / "+" separator choice (see the class comment) actually holds up.
    [Fact]
    public void ToString_Output_Reparses_To_An_Equivalent_Tree()
    {
        SummaryLayoutTree original = SummaryLayoutTree.CreateExample();
        SummaryLayout2 layout = SummaryLayout2.FromTree("My Dashboard", original);

        string iniText = layout.ToString();

        ConfigParser parser = new(iniText);
        parser.Parse();
        SummaryLayout2 reparsed = new(parser.Sections[0]);

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

        SummaryLayout2 layout = new(gridSection);

        Assert.False(layout.IsTreeLayout);
    }

    [Fact]
    public void IsTreeLayout_Is_False_For_An_Unrecognised_LayoutType_Value()
    {
        ConfigSection section = new ConfigSection("Something Else").Add("layout-type", "grid");
        SummaryLayout2 layout = new(section);

        Assert.False(layout.IsTreeLayout);
    }

    [Fact]
    public void ToTree_With_No_Backing_Section_Falls_Back_To_The_Example_Tree()
    {
        SummaryLayout2 layout = new();

        SummaryLayoutTree tree = layout.ToTree();

        Assert.Equal(SummaryLayoutTree.CreateExample().Nodes.Count, tree.Nodes.Count);
    }

    [Fact]
    public void ToTree_With_No_Node_Keys_Falls_Back_To_The_Example_Tree()
    {
        ConfigSection section = new ConfigSection("Empty").Add("layout-type", "tree");
        SummaryLayout2 layout = new(section);

        SummaryLayoutTree tree = layout.ToTree();

        Assert.Equal(SummaryLayoutTree.CreateExample().Nodes.Count, tree.Nodes.Count);
    }

    [Fact]
    public void Name_Reflects_The_Backing_Sections_Name()
    {
        SummaryLayout2 layout = SummaryLayout2.FromTree("Custom Name", SummaryLayoutTree.CreateExample());

        Assert.Equal("Custom Name", layout.Name);
    }
}
