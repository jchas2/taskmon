using Task.Monitor.Gui.Controls.Summary2.Layout;

namespace Task.Monitor.Tests.Gui.Controls.Summary2;

// Covers the tree's own operations, including Split/Remove/AdjustRatio/FindParentSplitId, which
// LayoutDesignerScreen drives. The panes hold plain names, since the tree never looks inside them.
// Example()'s shape throughout: Column(Row(Cpu[3], Row(Memory[5], Gpu[6])[4])[1], Process[2])[0],
// root id 0.
public sealed class LayoutTreeTests
{
    [Fact]
    public void AdjustRatio_Clamps_To_The_Valid_Range()
    {
        LayoutTree<string> tree = Example();

        Assert.True(tree.AdjustRatio(0, 10f));
        Assert.Equal(0.9f, tree.Nodes[0].Ratio);

        Assert.True(tree.AdjustRatio(0, -10f));
        Assert.Equal(0.1f, tree.Nodes[0].Ratio);
    }

    [Fact]
    public void AdjustRatio_Returns_False_For_A_Pane_Id()
    {
        LayoutTree<string> tree = Example();

        bool adjusted = tree.AdjustRatio(3, 0.1f); // node 3 is a pane, not a split

        Assert.False(adjusted);
    }

    [Fact]
    public void A_New_Tree_Is_One_Pane_At_The_Root()
    {
        LayoutTree<string> tree = new("Only");

        LayoutNode<string> pane = Assert.Single(tree.Nodes.Values);
        Assert.False(pane.IsSplit);
        Assert.Equal("Only", pane.Pane);
        Assert.Equal(tree.RootId, pane.Id);
        Assert.Equal(1, tree.NextId);
    }

    private static LayoutTree<string> Example() =>
        LayoutTree<string>.FromNodes([
            new LayoutNode<string> { Id = 0, IsSplit = true, Orientation = Orientation.Column, Ratio = 0.6f, FirstId = 1, SecondId = 2 },
            new LayoutNode<string> { Id = 1, IsSplit = true, Orientation = Orientation.Row, Ratio = 1f / 3f, FirstId = 3, SecondId = 4 },
            new LayoutNode<string> { Id = 2, Pane = "Process" },
            new LayoutNode<string> { Id = 3, Pane = "Cpu" },
            new LayoutNode<string> { Id = 4, IsSplit = true, Orientation = Orientation.Row, Ratio = 0.5f, FirstId = 5, SecondId = 6 },
            new LayoutNode<string> { Id = 5, Pane = "Memory" },
            new LayoutNode<string> { Id = 6, Pane = "Gpu" },
        ], rootId: 0);

    [Fact]
    public void FindParentSplitId_Returns_Null_For_The_Root()
    {
        LayoutTree<string> tree = Example();

        Assert.Null(tree.FindParentSplitId(0));
    }

    [Fact]
    public void FindParentSplitId_Returns_The_Direct_Parent()
    {
        LayoutTree<string> tree = Example();

        Assert.Equal(1, tree.FindParentSplitId(3));
        Assert.Equal(4, tree.FindParentSplitId(5));
        Assert.Equal(0, tree.FindParentSplitId(2));
    }

    [Fact]
    public void FromNodes_Refuses_A_Pane_With_Nothing_In_It()
    {
        LayoutNode<string>[] nodes = [
            new LayoutNode<string> { Id = 0, IsSplit = true, Orientation = Orientation.Row, FirstId = 1, SecondId = 2 },
            new LayoutNode<string> { Id = 1, Pane = "Cpu" },
            new LayoutNode<string> { Id = 2 },
        ];

        Assert.Throws<ArgumentException>(() => LayoutTree<string>.FromNodes(nodes, rootId: 0));
    }

    [Fact]
    public void Panes_Lists_Each_Pane_First_To_Last()
    {
        LayoutTree<string> tree = Example();

        Assert.Equal([3, 5, 6, 2], tree.Panes().Select(pane => pane.Id).ToArray());
        Assert.Equal(["Cpu", "Memory", "Gpu", "Process"], tree.Panes().Select(pane => pane.Pane).ToArray());
    }

    [Fact]
    public void Remove_Promotes_The_Sibling_Into_The_Parents_Id()
    {
        LayoutTree<string> tree = Example();

        // Remove node 6 (Gpu) - its parent is node 4 (Row split of Memory[5]/Gpu[6]), sibling is
        // node 5 (Memory), a leaf.
        bool removed = tree.Remove(6);

        Assert.True(removed);
        Assert.False(tree.Nodes.ContainsKey(6));
        Assert.False(tree.Nodes.ContainsKey(5));

        // Node 4 now IS the promoted Memory leaf.
        LayoutNode<string> promoted = tree.Nodes[4];
        Assert.False(promoted.IsSplit);
        Assert.Equal("Memory", promoted.Pane);

        // Whatever pointed at node 4 (node 1's SecondId) still does.
        Assert.Equal(4, tree.Nodes[1].SecondId);
    }

    [Fact]
    public void Remove_Refuses_A_Split_Node_Id()
    {
        LayoutTree<string> tree = Example();

        bool removed = tree.Remove(1); // node 1 is a split, not a pane

        Assert.False(removed);
        Assert.True(tree.Nodes.ContainsKey(1));
    }

    [Fact]
    public void Remove_Refuses_To_Remove_The_Root()
    {
        LayoutTree<string> tree = Example();

        bool removed = tree.Remove(tree.RootId);

        Assert.False(removed);
        Assert.True(tree.Nodes.ContainsKey(tree.RootId));
    }

    [Fact]
    public void Remove_When_The_Sibling_Is_Itself_A_Split_Preserves_Its_Whole_Subtree()
    {
        LayoutTree<string> tree = Example();

        // Remove node 2 (Process) - its parent is the root (0), sibling is node 1 (a Row split
        // with its own Cpu/Memory/Gpu subtree underneath).
        bool removed = tree.Remove(2);

        Assert.True(removed);
        Assert.False(tree.Nodes.ContainsKey(2));
        Assert.False(tree.Nodes.ContainsKey(1));

        LayoutNode<string> promoted = tree.Nodes[0];
        Assert.True(promoted.IsSplit);
        Assert.Equal(Orientation.Row, promoted.Orientation);
        Assert.Null(promoted.Pane);

        // The subtree's own leaves (3, 5, 6) are untouched - only node 1's identity moved to 0.
        Assert.Equal("Cpu", tree.Nodes[3].Pane);
        Assert.Equal("Memory", tree.Nodes[5].Pane);
        Assert.Equal("Gpu", tree.Nodes[6].Pane);
    }

    [Fact]
    public void Split_Allocates_Fresh_Ids_Not_Already_In_The_Tree()
    {
        LayoutTree<string> tree = Example();
        HashSet<int> existingIds = tree.Nodes.Keys.ToHashSet();

        (int firstId, int secondId) = tree.Split(3, Orientation.Row, "New");

        Assert.DoesNotContain(firstId, existingIds);
        Assert.DoesNotContain(secondId, existingIds);
        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public void Split_Keeps_The_Original_Id_As_The_New_Split_Node()
    {
        LayoutTree<string> tree = Example();

        (int firstId, int secondId) = tree.Split(3, Orientation.Column, "New");

        LayoutNode<string> splitNode = tree.Nodes[3];
        Assert.True(splitNode.IsSplit);
        Assert.Equal(Orientation.Column, splitNode.Orientation);
        Assert.Equal(0.5f, splitNode.Ratio);
        Assert.Equal(firstId, splitNode.FirstId);
        Assert.Equal(secondId, splitNode.SecondId);
        Assert.Null(splitNode.Pane);

        // Whatever pointed at the old leaf (node 1's FirstId) still points at it - now a split.
        Assert.Equal(3, tree.Nodes[1].FirstId);
    }

    [Fact]
    public void Split_Moves_The_Panes_Content_First_And_Puts_The_New_Pane_Second()
    {
        LayoutTree<string> tree = Example();

        (int firstId, int secondId) = tree.Split(3, Orientation.Row, "New");

        Assert.Equal("Cpu", tree.Nodes[firstId].Pane);
        Assert.False(tree.Nodes[firstId].IsSplit);

        Assert.Equal("New", tree.Nodes[secondId].Pane);
        Assert.False(tree.Nodes[secondId].IsSplit);
    }

    [Fact]
    public void Split_Throws_When_The_Target_Is_Already_A_Split()
    {
        LayoutTree<string> tree = Example();

        Assert.Throws<InvalidOperationException>(() => tree.Split(1, Orientation.Row, "New"));
    }
}
