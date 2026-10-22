using System.Drawing;
using Task.Monitor.Extensions;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.Power;

namespace Task.Monitor.Gui.Controls.Thermals;

public sealed partial class ThermalsControl
{
    private string BuildPowerComponentLabel(PowerReading reading) => reading.Component switch {
        PowerComponent.Cpu          => "CPU",
        PowerComponent.Gpu          => GpuLabel(reading.ComponentId),
        PowerComponent.Disk         => DiskLabel(reading.ComponentId),
        PowerComponent.Battery      => "Battery",
        PowerComponent.NeuralEngine => "Neural Engine",
        PowerComponent.Memory       => "Memory",
        PowerComponent.Display      => "Display",
                                  _ => "System"
    };

    private static int PowerComponentOrder(PowerComponent component) => component switch {
        PowerComponent.System       => 0,
        PowerComponent.Cpu          => 1,
        PowerComponent.Gpu          => 2,
        PowerComponent.NeuralEngine => 3,
        PowerComponent.Memory       => 4,
        PowerComponent.Display      => 5,
        PowerComponent.Disk         => 6,
                                  _ => 7
    };

    private void RebuildPowerRows(PowerMetrics metrics)
    {
        powerView.Items.Clear();

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;

        foreach (PowerReading reading in metrics.Readings.OrderBy(reading => PowerComponentOrder(reading.Component))) {
            string watts = reading.IsRated
                ? $"{reading.Watts.ToWattText()} (rated)"
                : reading.Watts.ToWattText();

            powerView.Items.Add(new ListViewItem([BuildPowerComponentLabel(reading), reading.Rail, watts], background, foreground));
        }
    }
}
