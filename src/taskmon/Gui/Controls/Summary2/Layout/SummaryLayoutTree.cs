using Task.Monitor.Configuration;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

// A recursive binary split tree.
public sealed class SummaryLayoutTree
{
    public Dictionary<int, SummaryLayoutNode> Nodes { get; } = new();

    public int RootId { get; private set; }

    public int NextId { get; private set; }

    private int AddNode(SummaryLayoutNode node)
    {
        Nodes[node.Id] = node;
        NextId = Math.Max(NextId, node.Id + 1);
        return node.Id;
    }

    public static SummaryLayoutTree CreateExample()
    {
        SummaryLayoutTree tree = new();

        tree.AddNode(new SummaryLayoutNode {
            Id = 2, ControlType = PaneControlType.Process, ProcessColumns = Statistics.Process | Statistics.Pid | Statistics.Cpu | Statistics.Mem
        });
        tree.AddNode(new SummaryLayoutNode {
            Id = 3, ControlType = PaneControlType.Cpu
        });
        tree.AddNode(new SummaryLayoutNode {
            Id = 5, ControlType = PaneControlType.Memory
        });
        tree.AddNode(new SummaryLayoutNode {
            Id = 6, ControlType = PaneControlType.Gpu
        });
        tree.AddNode(new SummaryLayoutNode {
            Id = 4, IsSplit = true, Orientation = Orientation.Row, Ratio = 0.5f, FirstId = 5, SecondId = 6
        });
        tree.AddNode(new SummaryLayoutNode {
            Id = 1, IsSplit = true, Orientation = Orientation.Row, Ratio = 1f / 3f, FirstId = 3, SecondId = 4
        });
        tree.AddNode(new SummaryLayoutNode {
            Id = 0, IsSplit = true, Orientation = Orientation.Column, Ratio = 0.6f, FirstId = 1, SecondId = 2
        });

        tree.RootId = 0;

        return tree;
    }

    public static SummaryLayoutTree CreateEmpty()
    {
        SummaryLayoutTree tree = new();

        tree.AddNode(new SummaryLayoutNode {
            Id = 0, ControlType = PaneControlType.Empty
        });
        
        tree.RootId = 0;

        return tree;
    }

    public static SummaryLayoutTree FromNodes(IEnumerable<SummaryLayoutNode> nodes, int rootId)
    {
        SummaryLayoutTree tree = new();

        foreach (SummaryLayoutNode node in nodes) {
            tree.AddNode(node);
        }

        tree.RootId = rootId;

        return tree;
    }

    // Returns every pane (leaf) node in the tree.
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

    public int? FindParentSplitId(int nodeId) =>
        Nodes
            .Where(kv => kv.Value.IsSplit && (kv.Value.FirstId == nodeId || kv.Value.SecondId == nodeId))
            .Select(kv => (int?)kv.Key)
            .FirstOrDefault();

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

    public bool AdjustRatio(int splitId, float delta)
    {
        if (!Nodes.TryGetValue(splitId, out SummaryLayoutNode? node) || !node.IsSplit) {
            return false;
        }

        node.Ratio = Math.Clamp(node.Ratio + delta, 0.1f, 0.9f);
        return true;
    }
}
