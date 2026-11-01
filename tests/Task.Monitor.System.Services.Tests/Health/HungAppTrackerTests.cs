using Task.Monitor.System.Services.Health;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class HungAppTrackerTests
{
    private static readonly DateTime Start = new(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc);

    private readonly HungAppTracker tracker = new();

    [Fact]
    public void A_Hang_Counts_From_When_It_Was_First_Seen()
    {
        tracker.Update([7], Name, Start);

        HungApp app = Assert.Single(tracker.Update([7], Name, Start.AddSeconds(12)));

        Assert.Equal(12.0, app.Seconds);
        Assert.Equal("app7", app.Name);
    }

    [Fact]
    public void A_Recovered_App_Starts_Again_If_It_Hangs_Later()
    {
        tracker.Update([7], Name, Start);
        Assert.Empty(tracker.Update([], Name, Start.AddSeconds(10)));

        HungApp app = Assert.Single(tracker.Update([7], Name, Start.AddSeconds(20)));

        Assert.Equal(0.0, app.Seconds);
    }

    private static string Name(int pid) => $"app{pid}";
}
