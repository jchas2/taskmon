using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls.Performance;

/// <summary>
/// A per-subsystem (and, for GPU / disk / network, per-device) detail pane. Every instance is fed
/// <see cref="Sample"/> on every snapshot so its charts keep a gap-free history even while another
/// pane is on screen; only the active one is drawn.
/// </summary>
internal interface IPerformanceDetail
{
    void Sample(SystemSnapshot snapshot);
}
