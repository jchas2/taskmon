using Task.Monitor.System.Services.Health;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class VolumeTrendTrackerTests
{
    private const long Megabyte = 1024L * 1024L;

    private static readonly DateTime Start = new(2026, 11, 2, 2, 0, 0, DateTimeKind.Utc);

    private readonly VolumeTrendTracker tracker = new();

    [Fact]
    public void A_Drive_Plugged_Back_In_Starts_From_Fresh()
    {
        Read("E:\\", freeMb: 1000, minutes: 0);
        Read("E:\\", freeMb: 900, minutes: 3);

        tracker.Update([], Start.AddMinutes(4));

        Assert.Null(Read("E:\\", freeMb: 500, minutes: 5).FillBytesPerMinute);
    }

    [Fact]
    public void A_Drive_That_Is_Filling_Has_A_Positive_Rate()
    {
        Read("E:\\", freeMb: 1000, minutes: 0);

        HealthVolume volume = Read("E:\\", freeMb: 700, minutes: 3);

        Assert.Equal(100.0 * Megabyte, volume.FillBytesPerMinute);
    }

    [Fact]
    public void A_Drive_That_Is_Freeing_Space_Has_A_Negative_Rate()
    {
        Read("E:\\", freeMb: 700, minutes: 0);

        Assert.Equal(-100.0 * Megabyte, Read("E:\\", freeMb: 1000, minutes: 3).FillBytesPerMinute);
    }

    [Fact]
    public void A_Drive_That_Is_Not_Changing_Has_No_Rate()
    {
        Read("E:\\", freeMb: 1000, minutes: 0);

        Assert.Equal(0.0, Read("E:\\", freeMb: 1000, minutes: 5).FillBytesPerMinute);
    }

    [Fact]
    public void Old_Readings_Drop_Out_So_A_Finished_Copy_Stops_Reading_As_Filling()
    {
        Read("E:\\", freeMb: 2000, minutes: 0);
        Read("E:\\", freeMb: 1000, minutes: 1);

        for (int minutes = 2; minutes <= 12; minutes++) {
            Read("E:\\", freeMb: 1000, minutes: minutes);
        }

        Assert.Equal(0.0, Read("E:\\", freeMb: 1000, minutes: 13).FillBytesPerMinute);
    }

    private HealthVolume Read(string name, long freeMb, double minutes) =>
        tracker.Update([new HealthVolume(name, freeMb * Megabyte, 10_000 * Megabyte, false)], Start.AddMinutes(minutes))[0];

    [Fact]
    public void There_Is_No_Rate_Until_There_Is_Enough_History()
    {
        Read("E:\\", freeMb: 1000, minutes: 0);

        Assert.Null(Read("E:\\", freeMb: 900, minutes: 1).FillBytesPerMinute);
    }

    private static HealthVolume Volume(string name, long freeMb) =>
        new(name, freeMb * Megabyte, 10_000 * Megabyte, false);

    [Fact]
    public void Volumes_Are_Tracked_Separately()
    {
        DateTime later = Start.AddMinutes(4);

        tracker.Update([Volume("C:\\", 1000), Volume("E:\\", 1000)], Start);

        IReadOnlyList<HealthVolume> volumes = tracker.Update([Volume("C:\\", 1000), Volume("E:\\", 600)], later);

        Assert.Equal(0.0, volumes[0].FillBytesPerMinute);
        Assert.Equal(100.0 * Megabyte, volumes[1].FillBytesPerMinute);
    }
}
