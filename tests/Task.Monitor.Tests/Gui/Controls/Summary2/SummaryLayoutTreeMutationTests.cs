using Task.Monitor.Gui.Controls.Summary2.Layout;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

// Covers Split/Remove/AdjustRatio/FindParentSplitId - the mutating operations LayoutDesignerScreen
// drives. CreateExample()'s shape throughout: Column(Row(Cpu[3], Row(Memory[5], Gpu[6])[4])[1],
// Process[2])[0], root id 0.
public sealed class SummaryLayoutTreeMutationTests
{
    [Fact]
    public void FindParentSplitId_Returns_The_Direct_Parent()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

        Assert.Equal(1, tree.FindParentSplitId(3));
        Assert.Equal(4, tree.FindParentSplitId(5));
        Assert.Equal(0, tree.FindParentSplitId(2));
    }

    [Fact]
    public void FindParentSplitId_Returns_Null_For_The_Root()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

        Assert.Null(tree.FindParentSplitId(0));
    }

    [Fact]
    public void Split_Keeps_The_Original_Id_As_The_New_Split_Node()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

        (int firstId, int secondId) = tree.Split(3, Orientation.Column);

        SummaryLayoutNode splitNode = tree.Nodes[3];
        Assert.True(splitNode.IsSplit);
        Assert.Equal(Orientation.Column, splitNode.Orientation);
        Assert.Equal(0.5f, splitNode.Ratio);
        Assert.Equal(firstId, splitNode.FirstId);
        Assert.Equal(secondId, splitNode.SecondId);

        // Whatever pointed at the old leaf (node 1's FirstId) still points at it - now a split.
        Assert.Equal(3, tree.Nodes[1].FirstId);
    }

    [Fact]
    public void Split_Preserves_The_Original_Panes_Content_On_The_First_Child()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

        (int firstId, int secondId) = tree.Split(3, Orientation.Row);

        Assert.Equal(PaneControlType.Cpu, tree.Nodes[firstId].ControlType);
        Assert.False(tree.Nodes[firstId].IsSplit);

        Assert.Equal(PaneControlType.Empty, tree.Nodes[secondId].ControlType);
        Assert.False(tree.Nodes[secondId].IsSplit);
    }

    [Fact]
    public void Split_Throws_When_The_Target_Is_Already_A_Split()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

        Assert.Throws<InvalidOperationException>(() => tree.Split(1, Orientation.Row));
    }

    [Fact]
    public void Split_Allocates_Fresh_Ids_Not_Already_In_The_Tree()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();
        HashSet<int> existingIds = tree.Nodes.Keys.ToHashSet();

        (int firstId, int secondId) = tree.Split(3, Orientation.Row);

        Assert.DoesNotContain(firstId, existingIds);
        Assert.DoesNotContain(secondId, existingIds);
        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public void Remove_Promotes_The_Sibling_Into_The_Parents_Id()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

        // Remove node 6 (Gpu) - its parent is node 4 (Row split of Memory[5]/Gpu[6]), sibling is
        // node 5 (Memory), a leaf.
        bool removed = tree.Remove(6);

        Assert.True(removed);
        Assert.False(tree.Nodes.ContainsKey(6));
        Assert.False(tree.Nodes.ContainsKey(5));

        // Node 4 now IS the promoted Memory leaf.
        SummaryLayoutNode promoted = tree.Nodes[4];
        Assert.False(promoted.IsSplit);
        Assert.Equal(PaneControlType.Memory, promoted.ControlType);

        // Whatever pointed at node 4 (node 1's SecondId) still does.
        Assert.Equal(4, tree.Nodes[1].SecondId);
    }

    [Fact]
    public void Remove_Refuses_To_Remove_The_Root()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

        bool removed = tree.Remove(tree.RootId);

        Assert.False(removed);
        Assert.True(tree.Nodes.ContainsKey(tree.RootId));
    }

    [Fact]
    public void Remove_Refuses_A_Split_Node_Id()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

        bool removed = tree.Remove(1); // node 1 is a split, not a pane

        Assert.False(removed);
        Assert.True(tree.Nodes.ContainsKey(1));
    }

    [Fact]
    public void Remove_When_The_Sibling_Is_Itself_A_Split_Preserves_Its_Whole_Subtree()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

        // Remove node 2 (Process) - its parent is the root (0), sibling is node 1 (a Row split
        // with its own Cpu/Memory/Gpu subtree underneath).
        bool removed = tree.Remove(2);

        Assert.True(removed);
        Assert.False(tree.Nodes.ContainsKey(2));
        Assert.False(tree.Nodes.ContainsKey(1));

        SummaryLayoutNode promoted = tree.Nodes[0];
        Assert.True(promoted.IsSplit);
        Assert.Equal(Orientation.Row, promoted.Orientation);

        // The subtree's own leaves (3, 5, 6) are untouched - only node 1's identity moved to 0.
        Assert.Equal(PaneControlType.Cpu, tree.Nodes[3].ControlType);
        Assert.Equal(PaneControlType.Memory, tree.Nodes[5].ControlType);
        Assert.Equal(PaneControlType.Gpu, tree.Nodes[6].ControlType);
    }

    [Fact]
    public void AdjustRatio_Clamps_To_The_Valid_Range()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

        Assert.True(tree.AdjustRatio(0, 10f));
        Assert.Equal(0.9f, tree.Nodes[0].Ratio);

        Assert.True(tree.AdjustRatio(0, -10f));
        Assert.Equal(0.1f, tree.Nodes[0].Ratio);
    }

    [Fact]
    public void AdjustRatio_Returns_False_For_A_Pane_Id()
    {
        SummaryLayoutTree tree = SummaryLayoutTree.CreateExample();

        bool adjusted = tree.AdjustRatio(3, 0.1f); // node 3 is a pane, not a split

        Assert.False(adjusted);
    }
}
