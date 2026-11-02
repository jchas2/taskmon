namespace Task.Monitor.System.Services.Health;

internal sealed class VolumeTrendTracker
{
    private const double MinimumMinutes = 2.0;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly Dictionary<string, Queue<(DateTime TimeUtc, long FreeBytes)>> history = new();

    private double? FillRate(HealthVolume volume, DateTime utcNow)
    {
        if (!history.TryGetValue(volume.Name, out Queue<(DateTime TimeUtc, long FreeBytes)>? readings)) {
            readings = history[volume.Name] = new();
        }

        readings.Enqueue((utcNow, volume.FreeBytes));

        while (utcNow - readings.Peek().TimeUtc > Window) {
            readings.Dequeue();
        }

        (DateTime oldestUtc, long oldestFree) = readings.Peek();

        double minutes = (utcNow - oldestUtc).TotalMinutes;

        return minutes >= MinimumMinutes
            ? (oldestFree - volume.FreeBytes) / minutes
            : null;
    }

    public IReadOnlyList<HealthVolume> Update(IReadOnlyList<HealthVolume> volumes, DateTime utcNow)
    {
        foreach (string gone in history.Keys.Except(volumes.Select(volume => volume.Name)).ToList()) {
            history.Remove(gone);
        }

        return volumes
            .Select(volume => volume with { FillBytesPerMinute = FillRate(volume, utcNow) })
            .ToList();
    }
}
