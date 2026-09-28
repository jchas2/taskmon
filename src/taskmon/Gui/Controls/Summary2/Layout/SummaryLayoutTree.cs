using Task.Monitor.Configuration;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

// A recursive binary split tree: every split has exactly two children (an N-way row is just
// N-1 nested Row splits, the same representation tmux/i3 use internally) which keeps split/merge
// operations simple - always "split *this* pane in two" - rather than needing variable-arity
// child lists.
public sealed class SummaryLayoutTree
{
    public Dictionary<int, SummaryLayoutNode> Nodes { get; } = new();

    public int RootId { get; private set; }

    // The next unused node id - tracked so Split() can allocate fresh ids without ever needing
    // to rescan Nodes.Keys for a gap.
    public int NextId { get; private set; }

    private int AddNode(SummaryLayoutNode node)
    {
        Nodes[node.Id] = node;
        NextId = Math.Max(NextId, node.Id + 1);
        return node.Id;
    }

    // Three chart panes in a row (Cpu | Memory | Gpu, each a third of the width) over one
    // Process pane showing a reduced column set - the first mockup from the design discussion.
    // Stands in for a real saved layout until Milestone 2 adds persistence.
    public static SummaryLayoutTree CreateExample()
    {
        SummaryLayoutTree tree = new();

        tree.AddNode(new SummaryLayoutNode { Id = 2, ControlType = PaneControlType.Process, ProcessColumns = Statistics.Process | Statistics.Pid | Statistics.Cpu | Statistics.Mem });
        tree.AddNode(new SummaryLayoutNode { Id = 3, ControlType = PaneControlType.Cpu });
        tree.AddNode(new SummaryLayoutNode { Id = 5, ControlType = PaneControlType.Memory });
        tree.AddNode(new SummaryLayoutNode { Id = 6, ControlType = PaneControlType.Gpu });
        tree.AddNode(new SummaryLayoutNode { Id = 4, IsSplit = true, Orientation = Orientation.Row, Ratio = 0.5f, FirstId = 5, SecondId = 6 });
        tree.AddNode(new SummaryLayoutNode { Id = 1, IsSplit = true, Orientation = Orientation.Row, Ratio = 1f / 3f, FirstId = 3, SecondId = 4 });
        tree.AddNode(new SummaryLayoutNode { Id = 0, IsSplit = true, Orientation = Orientation.Column, Ratio = 0.6f, FirstId = 1, SecondId = 2 });

        tree.RootId = 0;

        return tree;
    }

    // A single blank pane filling the whole area - the starting point for a new layout in the
    // designer, which the user then splits and assigns. CreateExample stays the dashboard's
    // fallback when no saved layout exists.
    public static SummaryLayoutTree CreateEmpty()
    {
        SummaryLayoutTree tree = new();

        tree.AddNode(new SummaryLayoutNode { Id = 0, ControlType = PaneControlType.Empty });
        tree.RootId = 0;

        return tree;
    }

    // Rebuilds a tree from a flat node list - what SummaryControlLayout.ToTree() uses to turn a parsed
    // .layout file back into a tree, since AddNode/RootId are otherwise only ever set by this
    // class itself (CreateExample, and Split/Remove below).
    public static SummaryLayoutTree FromNodes(IEnumerable<SummaryLayoutNode> nodes, int rootId)
    {
        SummaryLayoutTree tree = new();

        foreach (SummaryLayoutNode node in nodes) {
            tree.AddNode(node);
        }

        tree.RootId = rootId;

        return tree;
    }

    // Every pane (leaf) node in the tree, in traversal order - the order panes are first
    // encountered when walking from the root, which is what "first pane in tree order" (the
    // default initial focus) means.
    public IEnumerable<SummaryLayoutNode> Panes()
    {
        foreach (int nodeId in TraversalOrder(RootId)) {
            if (Nodes[nodeId] is { IsSplit: false } pane) {
                yield return pane;
            }
        }
    }

    private IEnumerable<int> TraversalOrder(int nodeId)
    {
        SummaryLayoutNode node = Nodes[nodeId];

        if (!node.IsSplit) {
            yield return nodeId;
            yield break;
        }

        foreach (int id in TraversalOrder(node.FirstId)) {
            yield return id;
        }

        foreach (int id in TraversalOrder(node.SecondId)) {
            yield return id;
        }
    }

    // The split node that directly contains nodeId as a FirstId/SecondId child, or null if
    // nodeId is the root (which has no parent) or isn't in the tree. An O(n) scan rather than
    // tracked parent pointers - trees stay small (a handful of panes), and this keeps
    // SummaryLayoutNode itself a plain, independently-serialisable value with no back-references.
    public int? FindParentSplitId(int nodeId) =>
        Nodes
            .Where(kv => kv.Value.IsSplit && (kv.Value.FirstId == nodeId || kv.Value.SecondId == nodeId))
            .Select(kv => (int?)kv.Key)
            .FirstOrDefault();

    // Splits an existing leaf pane in two along orientation: paneId keeps its id but becomes the
    // new split node (so whatever pointed at it - a parent's FirstId/SecondId, or RootId - stays
    // valid without needing to be found and rewritten), holding two freshly minted leaves - one
    // carrying the original pane's content, the other a blank Empty pane ready to be assigned.
    // Returns (originalContentId, newEmptyPaneId).
    public (int FirstId, int SecondId) Split(int paneId, Orientation orientation)
    {
        SummaryLayoutNode original = Nodes[paneId];

        if (original.IsSplit) {
            throw new InvalidOperationException($"Node {paneId} is already a split - only a pane (leaf) can be split.");
        }

        int firstId = AddNode(new SummaryLayoutNode {
            Id = NextId,
            ControlType = original.ControlType,
            ProcessColumns = original.ProcessColumns,
        });

        int secondId = AddNode(new SummaryLayoutNode {
            Id = NextId,
            ControlType = PaneControlType.Empty,
        });

        Nodes[paneId] = new SummaryLayoutNode {
            Id = paneId,
            IsSplit = true,
            Orientation = orientation,
            Ratio = 0.5f,
            FirstId = firstId,
            SecondId = secondId,
        };

        return (firstId, secondId);
    }

    // Removes a leaf pane, promoting its sibling into their parent split's place (which keeps the
    // parent's id, so a grandparent's FirstId/SecondId - or RootId - never needs rewriting). No-op
    // (returns false) for the root pane, since there both is no parent to merge into and nothing
    // left to hold the position (a single-pane tree can't shrink any further).
    public bool Remove(int paneId)
    {
        if (paneId == RootId || !Nodes.TryGetValue(paneId, out SummaryLayoutNode? pane) || pane.IsSplit) {
            return false;
        }

        int? parentId = FindParentSplitId(paneId);

        if (parentId is not { } pid) {
            return false;
        }

        SummaryLayoutNode parent = Nodes[pid];
        int siblingId = parent.FirstId == paneId ? parent.SecondId : parent.FirstId;
        SummaryLayoutNode sibling = Nodes[siblingId];

        Nodes[pid] = new SummaryLayoutNode {
            Id = pid,
            IsSplit = sibling.IsSplit,
            Orientation = sibling.Orientation,
            Ratio = sibling.Ratio,
            FirstId = sibling.FirstId,
            SecondId = sibling.SecondId,
            ControlType = sibling.ControlType,
            ProcessColumns = sibling.ProcessColumns,
        };

        Nodes.Remove(paneId);
        Nodes.Remove(siblingId);

        return true;
    }

    // Adjusts a split's Ratio (First's share of the space) in place, clamped so neither side can
    // be squeezed to nothing.
    public bool AdjustRatio(int splitId, float delta)
    {
        if (!Nodes.TryGetValue(splitId, out SummaryLayoutNode? node) || !node.IsSplit) {
            return false;
        }

        node.Ratio = Math.Clamp(node.Ratio + delta, 0.1f, 0.9f);
        return true;
    }
}
