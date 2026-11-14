namespace Task.Monitor.Gui.Controls.Summary2.Layout;

// A recursive binary split tree. Each split divides its area between two children by its Ratio,
// and each pane (leaf) holds a T.
public sealed class LayoutTree<T> where T : class
{
    private LayoutTree() { }

    public LayoutTree(T rootPane)
    {
        AddNode(new LayoutNode<T> {
            Id = 0, Pane = rootPane
        });

        RootId = 0;
    }

    private int AddNode(LayoutNode<T> node)
    {
        Nodes[node.Id] = node;
        NextId = Math.Max(NextId, node.Id + 1);
        return node.Id;
    }

    public bool AdjustRatio(int splitId, float delta)
    {
        if (!Nodes.TryGetValue(splitId, out LayoutNode<T>? node) || !node.IsSplit) {
            return false;
        }

        node.Ratio = Math.Clamp(node.Ratio + delta, 0.1f, 0.9f);
        return true;
    }

    public int? FindParentSplitId(int nodeId) =>
        Nodes
            .Where(kv => kv.Value.IsSplit && (kv.Value.FirstId == nodeId || kv.Value.SecondId == nodeId))
            .Select(kv => (int?)kv.Key)
            .FirstOrDefault();

    public static LayoutTree<T> FromNodes(IEnumerable<LayoutNode<T>> nodes, int rootId)
    {
        LayoutTree<T> tree = new();

        foreach (LayoutNode<T> node in nodes) {
            if (!node.IsSplit && node.Pane is null) {
                throw new ArgumentException($"Pane {node.Id} has nothing in it.", nameof(nodes));
            }

            tree.AddNode(node);
        }

        tree.RootId = rootId;

        return tree;
    }

    public int NextId { get; private set; }

    public Dictionary<int, LayoutNode<T>> Nodes { get; } = new();

    // Returns every pane (leaf) in the tree, first to last.
    public IEnumerable<(int Id, T Pane)> Panes()
    {
        foreach (int nodeId in TraversalOrder(RootId)) {
            LayoutNode<T> node = Nodes[nodeId];

            if (!node.IsSplit) {
                yield return (nodeId, node.Pane ?? throw new InvalidOperationException($"Pane {nodeId} has nothing in it."));
            }
        }
    }

    public bool Remove(int paneId)
    {
        if (paneId == RootId || !Nodes.TryGetValue(paneId, out LayoutNode<T>? pane) || pane.IsSplit) {
            return false;
        }

        int? parentId = FindParentSplitId(paneId);

        if (parentId is not { } pid) {
            return false;
        }

        LayoutNode<T> parent = Nodes[pid];

        int siblingId = parent.FirstId == paneId
            ? parent.SecondId
            : parent.FirstId;

        LayoutNode<T> sibling = Nodes[siblingId];

        Nodes[pid] = new LayoutNode<T> {
            Id          = pid,
            IsSplit     = sibling.IsSplit,
            Orientation = sibling.Orientation,
            Ratio       = sibling.Ratio,
            FirstId     = sibling.FirstId,
            SecondId    = sibling.SecondId,
            Pane        = sibling.Pane,
        };

        Nodes.Remove(paneId);
        Nodes.Remove(siblingId);

        return true;
    }

    public int RootId { get; private set; }

    // The pane's id becomes the split. Its first child keeps what the pane held, and the second
    // holds newPane.
    public (int FirstId, int SecondId) Split(int paneId, Orientation orientation, T newPane)
    {
        LayoutNode<T> original = Nodes[paneId];

        if (original.IsSplit) {
            throw new InvalidOperationException($"Node {paneId} is already a split - only a pane (leaf) can be split.");
        }

        int firstId = AddNode(new LayoutNode<T> {
            Id   = NextId,
            Pane = original.Pane,
        });

        int secondId = AddNode(new LayoutNode<T> {
            Id   = NextId,
            Pane = newPane,
        });

        Nodes[paneId] = new LayoutNode<T> {
            Id          = paneId,
            IsSplit     = true,
            Orientation = orientation,
            Ratio       = 0.5f,
            FirstId     = firstId,
            SecondId    = secondId,
        };

        return (firstId, secondId);
    }

    private IEnumerable<int> TraversalOrder(int nodeId)
    {
        LayoutNode<T> node = Nodes[nodeId];

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
}
