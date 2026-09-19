using System.Globalization;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Configuration;

namespace Task.Monitor.Configuration;

// The persisted (.layout file) form of a SummaryLayoutTree - parallel to Layout (the fixed-grid
// format) rather than replacing it, so existing grid .layout files keep parsing exactly as
// before. Both live in the same layouts folder with the same .layout extension; LayoutType is
// what tells a parsed ConfigSection apart as one or the other (see IsTreeLayout and
// AppConfig.LoadLayouts).
//
// A tree round-trips as (using "," between a node's own fields and "+" between the entries of a
// Process pane's column list - ';' is a comment marker to ConfigParser, so it can't be used here):
//   [My Dashboard]
//   layout-type=tree
//   root=0
//   nodes=0,1,2
//   node.0=split,row,0.5,1,2
//   node.1=pane,cpu
//   node.2=pane,process,process+pid+cpu+mem
public sealed class SummaryLayout2
{
    private const string LayoutTypeTree = "tree";
    private const char FieldSeparator = ',';
    private const char ColumnSeparator = '+';

    private ConfigSection? layoutSection;

    public SummaryLayout2() { }

    public SummaryLayout2(ConfigSection configSection) => layoutSection = configSection;

    public string Name => layoutSection?.Name ?? string.Empty;

    public void Update(ConfigSection configSection) => layoutSection = configSection;

    // False for a section with no layout-type key (an ordinary grid Layout file) or any value
    // other than "tree" - what keeps the two layout kinds from being misparsed as one another.
    public bool IsTreeLayout =>
        layoutSection?.GetString(Constants.Keys.LayoutType, string.Empty) == LayoutTypeTree;

    public SummaryLayoutTree ToTree()
    {
        if (layoutSection == null) {
            return SummaryLayoutTree.CreateExample();
        }

        string nodesCsv = layoutSection.GetString(Constants.Keys.SummaryNodes, string.Empty);

        List<int> nodeIds = nodesCsv
            .Split(FieldSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(part => int.TryParse(part, out _))
            .Select(int.Parse)
            .ToList();

        List<SummaryLayoutNode> nodes = new();

        foreach (int id in nodeIds) {
            string raw = layoutSection.GetString($"{Constants.Keys.SummaryNodePrefix}{id}", string.Empty);

            if (ParseNode(id, raw) is { } node) {
                nodes.Add(node);
            }
        }

        if (nodes.Count == 0) {
            return SummaryLayoutTree.CreateExample();
        }

        int rootId = layoutSection.GetInt(Constants.Keys.SummaryRoot, nodes[0].Id);

        return SummaryLayoutTree.FromNodes(nodes, rootId);
    }

    private static SummaryLayoutNode? ParseNode(int id, string raw)
    {
        string[] parts = raw.Split(FieldSeparator);

        if (parts.Length >= 5 && parts[0] == "split") {
            return new SummaryLayoutNode {
                Id = id,
                IsSplit = true,
                Orientation = Enum.TryParse(parts[1], ignoreCase: true, out Orientation orientation)
                    ? orientation
                    : Orientation.Row,
                Ratio = float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ratio)
                    ? ratio
                    : 0.5f,
                FirstId = int.TryParse(parts[3], out int firstId) ? firstId : 0,
                SecondId = int.TryParse(parts[4], out int secondId) ? secondId : 0,
            };
        }

        if (parts.Length >= 2 && parts[0] == "pane") {
            return new SummaryLayoutNode {
                Id = id,
                ControlType = Enum.TryParse(parts[1], ignoreCase: true, out PaneControlType controlType)
                    ? controlType
                    : PaneControlType.Empty,
                ProcessColumns = parts.Length >= 3 ? ParseColumns(parts[2]) : null,
            };
        }

        return null;
    }

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

    public static SummaryLayout2 FromTree(string name, SummaryLayoutTree tree)
    {
        ConfigSection section = new(name);

        section.Add(Constants.Keys.LayoutType, LayoutTypeTree);
        section.Add(Constants.Keys.SummaryRoot, tree.RootId.ToString(CultureInfo.InvariantCulture));
        section.Add(Constants.Keys.SummaryNodes, string.Join(FieldSeparator, tree.Nodes.Keys));

        foreach ((int id, SummaryLayoutNode node) in tree.Nodes) {
            section.Add($"{Constants.Keys.SummaryNodePrefix}{id}", SerializeNode(node));
        }

        return new SummaryLayout2(section);
    }

    private static string SerializeNode(SummaryLayoutNode node) =>
        node.IsSplit
            ? string.Join(FieldSeparator, "split", node.Orientation.ToString(),
                node.Ratio.ToString(CultureInfo.InvariantCulture), node.FirstId, node.SecondId)
            : node.ProcessColumns is { } columns
                ? string.Join(FieldSeparator, "pane", node.ControlType.ToString(), SerializeColumns(columns))
                : string.Join(FieldSeparator, "pane", node.ControlType.ToString());

    private static string SerializeColumns(Statistics columns) =>
        string.Join(ColumnSeparator, Enum.GetValues<Statistics>()
            .Where(flag => flag != 0 && columns.HasFlag(flag))
            .Select(flag => flag.ToString()));

    public override string ToString() => layoutSection?.ToString() ?? string.Empty;
}
