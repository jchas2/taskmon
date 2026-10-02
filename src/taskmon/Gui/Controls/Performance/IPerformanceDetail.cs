using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Performance;

internal interface IPerformanceDetail
{
    void Sample(SystemSnapshot snapshot);
}
