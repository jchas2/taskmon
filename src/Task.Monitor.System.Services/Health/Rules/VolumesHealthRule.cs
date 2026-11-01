namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class VolumesHealthRule : IHealthRule
{
    private const double BytesPerGigabyte = 1024.0 * 1024.0 * 1024.0;
    private const double PercentPerRatio  = 100.0;

    private static HealthCondition Condition(HealthVolume volume, HealthThresholds thresholds)
    {
        double freePercent = FreePercent(volume);
        string reason      = $"{volume.Name} {100.0 - freePercent:0}% full";

        HealthCondition byPercent = HealthCondition.Falling(
            freePercent,
            thresholds.VolumeFreeWarningPercent,
            thresholds.VolumeFreeCriticalPercent,
            reason);

        if (!volume.IsSystem) {
            return byPercent;
        }

        HealthCondition byBytes = HealthCondition.Falling(
            volume.FreeBytes / BytesPerGigabyte,
            double.NegativeInfinity,
            thresholds.SystemVolumeFreeCriticalGb,
            reason);

        return HealthCondition.Worst([byPercent, byBytes]);
    }

    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Volumes.Count == 0) {
            return new HealthReading { Resource = Resource };
        }

        HealthVolume    fullest = sample.Volumes.MinBy(FreePercent)!;
        HealthCondition worst   = HealthCondition.Worst(sample.Volumes.Select(volume => Condition(volume, thresholds)));

        return new HealthReading {
            Resource    = Resource,
            Level       = worst.Level,
            Reason      = worst.Reason,
            Severity    = worst.Severity,
            IsImmediate = true,
            Utilisation = $"{fullest.Name} {100.0 - FreePercent(fullest):0}% full"
        };
    }

    private static double FreePercent(HealthVolume volume) =>
        volume.TotalBytes > 0
            ? volume.FreeBytes / (double)volume.TotalBytes * PercentPerRatio
            : PercentPerRatio;

    public HealthResource Resource => HealthResource.Volumes;
}
