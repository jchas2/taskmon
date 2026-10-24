using System.Reflection;
using Task.Monitor.Gui.Controls.Thermals;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Thermal;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class FansControlTests
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public FansControlTests()
    {
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    [Fact]
    public void ComputeColumnWidths_Gives_The_Remainder_To_The_Load_Column()
    {
        Assert.Equal([10, 10, 10, 10, 13], FansControl.ComputeColumnWidths(56));
    }

    [Fact]
    public void ComputeColumnWidths_Shares_A_Narrow_Width_Evenly()
    {
        Assert.Equal([4, 4, 4, 4, 6], FansControl.ComputeColumnWidths(25));
    }

    [Fact]
    public void HeightFor_Fits_The_Header_Borders_And_One_Row_Per_Fan()
    {
        Assert.Equal(4, FansControl.HeightFor(0));
        Assert.Equal(5, FansControl.HeightFor(2));
    }

    private int SnapshotSubscriberCount()
    {
        MulticastDelegate? handler = (MulticastDelegate?)typeof(ServiceController)
            .GetField("SystemSnapshotUpdated", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(runContext.ServiceController);

        return handler?.GetInvocationList().Count(d => d.Target is FansControl) ?? 0;
    }

    [Fact]
    public void Updates_Itself_From_Snapshots_While_Loaded()
    {
        FansControl ctrl = new(runContext.ServiceController, new ForwardingTerminal(runContext.Terminal), runContext.AppConfig) {
            Width  = 56,
            Height = 5
        };

        ctrl.Load();
        ctrl.Resize();

        Assert.Equal(1, SnapshotSubscriberCount());

        ctrl.Sample(new SystemSnapshot {
            Thermal = new ThermalInfo {
                Metrics = new ThermalMetrics {
                    Fans = [
                        new FanReading { Name = "Fan 1", Rpm = 0,    MinRpm = 1350, MaxRpm = 5349 },
                        new FanReading { Name = "Fan 2", Rpm = 2900, MinRpm = 1458, MaxRpm = 5800 },
                    ]
                }
            }
        });

        Assert.Equal(2, ctrl.FansView.Items.Count);

        ctrl.Unload();

        Assert.Equal(0, SnapshotSubscriberCount());
    }
}
