using Task.Monitor.Configuration;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

public sealed class SummaryLayoutNode
{
    public required int Id { get; init; }
    public bool IsSplit { get; set; }
    public Orientation Orientation { get; set; }
    public float Ratio { get; set; } = 0.5f;
    public int FirstId { get; set; }
    public int SecondId { get; set; }
    public PaneControlType ControlType { get; set; } = PaneControlType.Empty;
    // Only valid when ControlType == Process.
    public Statistics? ProcessColumns { get; set; }
}
