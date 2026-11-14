using System.Globalization;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Configuration;

namespace Task.Monitor.Configuration;

public sealed class SummaryControlLayout
{
    private const string LayoutTypeTree  = "tree";
    private const char   FieldSeparator  = ',';
    private const char   ColumnSeparator = '+';

    private ConfigSection? layoutSection;

    public SummaryControlLayout() { }

    public SummaryControlLayout(ConfigSection configSection) => layoutSection = configSection;

    public static LayoutTree<SummaryPane> Fallback() =>
        LayoutTree<SummaryPane>.FromNodes([
            new LayoutNode<SummaryPane> { Id = 0, IsSplit = true, Orientation = Orientation.Column, Ratio = 0.6f, FirstId = 1, SecondId = 2 },
            new LayoutNode<SummaryPane> { Id = 1, IsSplit = true, Orientation = Orientation.Row, Ratio = 1f / 3f, FirstId = 3, SecondId = 4 },
            new LayoutNode<SummaryPane> { Id = 2, Pane = new(PaneControlType.Process, Statistics.Process | Statistics.Pid | Statistics.Cpu | Statistics.Mem) },
            new LayoutNode<SummaryPane> { Id = 3, Pane = new(PaneControlType.Cpu) },
            new LayoutNode<SummaryPane> { Id = 4, IsSplit = true, Orientation = Orientation.Row, Ratio = 0.5f, FirstId = 5, SecondId = 6 },
            new LayoutNode<SummaryPane> { Id = 5, Pane = new(PaneControlType.Memory) },
            new LayoutNode<SummaryPane> { Id = 6, Pane = new(PaneControlType.Gpu) },
        ], rootId: 0);

    public static SummaryControlLayout FromTree(string name, LayoutTree<SummaryPane> tree)
    {
        ConfigSection section = new(name);

        section.Add(Constants.Keys.LayoutType,   LayoutTypeTree);
        section.Add(Constants.Keys.SummaryRoot,  tree.RootId.ToString(CultureInfo.InvariantCulture));
        section.Add(Constants.Keys.SummaryNodes, string.Join(FieldSeparator, tree.Nodes.Keys));

        foreach ((int id, LayoutNode<SummaryPane> node) in tree.Nodes) {
            section.Add($"{Constants.Keys.SummaryNodePrefix}{id}", SerializeNode(node));
        }

        return new SummaryControlLayout(section);
    }

    public bool IsBuiltIn { get; init; }

    public bool IsTreeLayout =>
        layoutSection?.GetString(Constants.Keys.LayoutType, string.Empty) == LayoutTypeTree;

    public string Name => layoutSection?.Name ?? string.Empty;

    private static Statistics? ParseColumns(string raw)
    {
        if (string.IsNullOrEmpty(raw)) {
            return null;
        }

        Statistics result = 0;

        foreach (string part in raw.Split(ColumnSeparator, StringSplitOptions.RemoveEmptyEntries)) {
            if (Enum.TryParse(part, ignoreCase: true, out Statistics flag)) {
                result |= flag;
            }
        }

        return result == 0 ? null : result;
    }

    private static LayoutNode<SummaryPane>? ParseNode(int id, string raw)
    {
        string[] parts = raw.Split(FieldSeparator);

        if (parts.Length >= 5 && parts[0] == "split") {
            return new LayoutNode<SummaryPane> {
                Id          = id,
                IsSplit     = true,
                Orientation = Enum.TryParse(parts[1], ignoreCase: true, out Orientation orientation) ? orientation : Orientation.Row,
                Ratio       = float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ratio) ? ratio : 0.5f,
                FirstId     = int.TryParse(parts[3], out int firstId) ? firstId : 0,
                SecondId    = int.TryParse(parts[4], out int secondId) ? secondId : 0,
            };
        }

        if (parts.Length >= 2 && parts[0] == "pane") {
            return new LayoutNode<SummaryPane> {
                Id   = id,
                Pane = new SummaryPane(
                    Enum.TryParse(parts[1], ignoreCase: true, out PaneControlType controlType) ? controlType : PaneControlType.Empty,
                    parts.Length >= 3 ? ParseColumns(parts[2]) : null),
            };
        }

        return null;
    }

    private static string SerializeColumns(Statistics columns) =>
        string.Join(ColumnSeparator, Enum.GetValues<Statistics>()
            .Where(flag => flag != 0 && columns.HasFlag(flag))
            .Select(flag => flag.ToString()));

    private static string SerializeNode(LayoutNode<SummaryPane> node) =>
        node.IsSplit
            ? string.Join(FieldSeparator, "split", node.Orientation.ToString(),
                node.Ratio.ToString(CultureInfo.InvariantCulture), node.FirstId, node.SecondId)
            : SerializePane(node.Pane!);

    private static string SerializePane(SummaryPane pane) =>
        pane.ProcessColumns is { } columns
            ? string.Join(FieldSeparator, "pane", pane.ControlType.ToString(), SerializeColumns(columns))
            : string.Join(FieldSeparator, "pane", pane.ControlType.ToString());

    public override string ToString() => layoutSection?.ToString() ?? string.Empty;

    public LayoutTree<SummaryPane> ToTree()
    {
        if (layoutSection == null) {
            return Fallback();
        }

        string nodesCsv = layoutSection.GetString(Constants.Keys.SummaryNodes, string.Empty);

        List<int> nodeIds = nodesCsv
            .Split(FieldSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(part => int.TryParse(part, out _))
            .Select(int.Parse)
            .ToList();

        List<LayoutNode<SummaryPane>> nodes = new();

        foreach (int id in nodeIds) {
            string raw = layoutSection.GetString($"{Constants.Keys.SummaryNodePrefix}{id}", string.Empty);

            if (ParseNode(id, raw) is { } node) {
                nodes.Add(node);
            }
        }

        if (nodes.Count == 0) {
            return Fallback();
        }

        int rootId = layoutSection.GetInt(Constants.Keys.SummaryRoot, nodes[0].Id);

        return LayoutTree<SummaryPane>.FromNodes(nodes, rootId);
    }

    public void Update(ConfigSection configSection) => layoutSection = configSection;
}
