using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Performance;

internal interface IPerformanceDetail
{
    void Sample(SystemSnapshot snapshot);

    Chart Spark { get; }

    (string Top, string Bottom) Summary { get; }

    string Title { get; }
}
