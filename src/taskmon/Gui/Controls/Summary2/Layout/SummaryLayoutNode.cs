using Task.Monitor.Configuration;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

// One node in a SummaryLayoutTree - either a split (dividing a rect between two child node ids)
// or a pane (a leaf hosting one control). A single class with an IsSplit discriminator, rather
// than a split/pane subclass pair, mirrors Layout.cs's own flat style and keeps serialization
// (one node = one "node.N=" line) a straight round trip with no polymorphism to reconstruct.
public sealed class SummaryLayoutNode
{
    public required int Id { get; init; }

    public bool IsSplit { get; set; }

    // Split-only.
    public Orientation Orientation { get; set; }
    public float Ratio { get; set; } = 0.5f;
    public int FirstId { get; set; }
    public int SecondId { get; set; }

    // Pane-only.
    public PaneControlType ControlType { get; set; } = PaneControlType.Empty;

    // Only meaningful when ControlType == Process. Null means "use the app-wide
    // AppConfig.VisibleColumns setting" - same null-means-default convention as
    // ProcessControl.VisibleColumnsOverride, which this feeds directly.
    public Statistics? ProcessColumns { get; set; }
}
