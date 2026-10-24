#if __APPLE__
using Moq;
using Task.Monitor.Extensions;
using Task.Monitor.Gui.Controls.Performance;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class MemoryPerformanceControlTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public MemoryPerformanceControlTests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    [Fact]
    public void Draws_The_Swap_Rates()
    {
        MemoryPerformanceControl ctrl = new(new ForwardingTerminal(runContext.Terminal), runContext.AppConfig) {
            Width  = 160,
            Height = 40
        };

        ctrl.Load();
        ctrl.Resize();
        ctrl.Sample(new SystemSnapshot {
            Memory = new MemoryInfo {
                Metrics = new MemoryMetrics { TotalPhysical = 1, SwapInBytesPerSecond = 2048, SwapOutBytesPerSecond = 0 }
            }
        });
        ctrl.Draw();

        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Swap In"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains("Swap Out"))), Times.AtLeastOnce);
        runContextHelper.terminal.Verify(t => t.Write(It.Is<string>(s => s.Contains(2048.0.ToFormattedByteRate()))), Times.AtLeastOnce);

        ctrl.Unload();
    }
}
#endif
