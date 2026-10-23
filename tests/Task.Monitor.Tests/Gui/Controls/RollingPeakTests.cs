using Task.Monitor.Gui.Controls;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class RollingPeakTests
{
    [Fact]
    public void Add_Forgets_A_Peak_Older_Than_The_Window()
    {
        RollingPeak peak = new(window: 3, floor: 1);

        peak.Add(100);
        peak.Add(10);

        Assert.Equal(0.1, peak.Add(10), precision: 9);
        Assert.Equal(1.0, peak.Add(10), precision: 9);
    }

    [Fact]
    public void Add_Scales_Against_The_Floor_Until_Traffic_Exceeds_It()
    {
        RollingPeak peak = new(window: 10, floor: 100);

        Assert.Equal(0.25, peak.Add(25), precision: 9);
        Assert.Equal(1.0,  peak.Add(400), precision: 9);
        Assert.Equal(0.5,  peak.Add(200), precision: 9);
    }

    [Fact]
    public void Add_Treats_A_Negative_Value_As_Zero()
    {
        RollingPeak peak = new(window: 10, floor: 100);

        Assert.Equal(0.0, peak.Add(-50));
    }
}
