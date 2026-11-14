using Task.Monitor.Configuration;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

public sealed record SummaryPane(PaneControlType ControlType, Statistics? ProcessColumns = null)
{
    public static SummaryPane Empty { get; } = new(PaneControlType.Empty);
}
