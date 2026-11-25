using Task.Monitor.Configuration;

namespace Task.Monitor.Gui.Controls.Summary2.Layout;

public sealed record SummaryPane(
    PaneControlType ControlType,
    Statistics?     ProcessColumns = null,
    Statistics?     SortColumn     = null,
    bool            SortAscending  = false)
{
    public static SummaryPane Empty { get; } = new(PaneControlType.Empty);
}
