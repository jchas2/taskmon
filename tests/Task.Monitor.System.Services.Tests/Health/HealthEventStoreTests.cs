using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Services.Health;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class HealthEventStoreTests : IDisposable
{
    private const int Limit = 100;

    private static readonly DateTime Start = new(2026, 11, 1, 6, 3, 33, DateTimeKind.Utc);

    private readonly string           folder = Directory.CreateTempSubdirectory("taskmon-events").FullName;
    private readonly HealthEventStore store;

    public HealthEventStoreTests() => store = new HealthEventStore(new FileSystem(), folder);

    [Theory]
    [InlineData("[event]\nversion=2\nresource=Cpu\nlevel=Warning\nstart=2026-11-01T06:03:33.0000000Z\n")]
    [InlineData("[event]\nversion=1\nresource=Toaster\nlevel=Warning\nstart=2026-11-01T06:03:33.0000000Z\n")]
    [InlineData("[event]\nversion=1\nresource=Cpu\nlevel=Warning\nstart=yesterday\n")]
    [InlineData("[other]\nversion=1\n")]
    public void An_Event_It_Cannot_Read_Is_Skipped(string text)
    {
        File.WriteAllText(Path.Combine(folder, "20261101T060333Z-cpu-1.ini"), text);

        Assert.Empty(store.Load(Limit));
    }

    [Fact]
    public void An_Event_Saved_Again_Replaces_Its_File()
    {
        HealthEvent open = Event(Start) with { EndUtc = null, Level = HealthLevel.Warning };

        store.Save(open);
        store.Save(open with { Level = HealthLevel.Critical });
        store.Save(open with { Level = HealthLevel.Critical, EndUtc = Start.AddMinutes(3) });

        HealthEvent loaded = Assert.Single(store.Load(Limit));

        Assert.Equal(HealthLevel.Critical, loaded.Level);
        Assert.False(loaded.IsInterrupted);
    }

    [Fact]
    public void An_Event_Saved_Before_It_Ended_Loads_As_Interrupted()
    {
        store.Save(Event(Start) with { EndUtc = null });

        HealthEvent loaded = Assert.Single(store.Load(Limit));

        Assert.True(loaded.IsInterrupted);
        Assert.Null(loaded.EndUtc);
    }

    [Theory]
    [InlineData("a;b")]
    [InlineData("a=b#c")]
    [InlineData(@"C:\ 98% full")]
    [InlineData(@"ends with a backslash\")]
    [InlineData(@"\s is not a semicolon")]
    [InlineData("line one\r\nline two")]
    [InlineData("CPU 94 °C near its 100 °C limit → held back")]
    [InlineData("[event]")]
    public void Awkward_Text_Survives_A_Round_Trip(string text)
    {
        store.Save(Event(Start) with { PeakReason = text, Culprits = [text] });

        HealthEvent loaded = Assert.Single(store.Load(Limit));

        Assert.Equal(text, loaded.PeakReason);
        Assert.Equal([text], loaded.Culprits);
    }

    [Fact]
    public void A_Corrupt_File_Is_Skipped_And_The_Others_Still_Load()
    {
        store.Save(Event(Start));
        File.WriteAllText(Path.Combine(folder, "20261101T060334Z-cpu-1.ini"), "not an ini file");

        Assert.Single(store.Load(Limit));
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private static HealthEvent Event(DateTime startUtc) =>
        new() {
            Resource   = HealthResource.Cpu,
            Level      = HealthLevel.Critical,
            StartUtc   = startUtc,
            EndUtc     = startUtc.AddMinutes(3),
            PeakReason = "100% busy",
            PeakValue  = 1.1111111111111112,
            Culprits   = ["yes", "Brave Browser H"]
        };

    [Fact]
    public void Every_Field_Survives_A_Round_Trip()
    {
        HealthEvent saved = Event(Start);

        store.Save(saved);

        HealthEvent loaded = Assert.Single(store.Load(Limit));

        Assert.Equal(saved.Resource, loaded.Resource);
        Assert.Equal(saved.Level, loaded.Level);
        Assert.Equal(saved.StartUtc, loaded.StartUtc);
        Assert.Equal(DateTimeKind.Utc, loaded.StartUtc.Kind);
        Assert.Equal(saved.EndUtc, loaded.EndUtc);
        Assert.Equal(saved.PeakReason, loaded.PeakReason);
        Assert.Equal(saved.PeakValue, loaded.PeakValue);
        Assert.Equal(saved.Culprits, loaded.Culprits);
        Assert.False(loaded.IsInterrupted);
    }

    [Fact]
    public void Load_Returns_The_Newest_Events_Oldest_First()
    {
        for (int minutes = 0; minutes < 5; minutes++) {
            store.Save(Event(Start.AddMinutes(minutes)));
        }

        Assert.Equal(
            [Start.AddMinutes(3), Start.AddMinutes(4)],
            store.Load(2).Select(healthEvent => healthEvent.StartUtc));
    }

    [Fact]
    public void Prune_Deletes_The_Oldest_Events()
    {
        for (int minutes = 0; minutes < 5; minutes++) {
            store.Save(Event(Start.AddMinutes(minutes)));
        }

        store.Prune(2);

        Assert.Equal(
            [Start.AddMinutes(3), Start.AddMinutes(4)],
            store.Load(Limit).Select(healthEvent => healthEvent.StartUtc));
    }

    [Fact]
    public void Prune_Leaves_Other_Files_Alone()
    {
        string other = Path.Combine(folder, "another-programs-settings.ini");

        File.WriteAllText(other, "[settings]\n");
        store.Save(Event(Start));

        store.Prune(0);

        Assert.True(File.Exists(other));
        Assert.Empty(store.Load(Limit));
    }

    [Fact]
    public void Two_Copies_Of_Taskmon_Would_Not_Share_A_File_Name()
    {
        store.Save(Event(Start));

        string name = Path.GetFileName(Assert.Single(Directory.GetFiles(folder)));

        Assert.Equal($"20261101T060333Z-cpu-{Environment.ProcessId}.ini", name);
    }
}
