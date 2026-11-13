namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class VolumesHealthRule : IHealthRule
{
    private const double BytesPerGigabyte = 1024.0 * 1024.0 * 1024.0;
    private const double PercentPerRatio  = 100.0;

    private static HealthCondition Condition(HealthVolume volume, HealthThresholds thresholds)
    {
        double      freePercent = FreePercent(volume);
        HealthCause cause       = new(HealthCauseKind.VolumeFull, PercentPerRatio - freePercent, volume.Name);

        HealthCondition byPercent = HealthCondition.Falling(
            freePercent,
            thresholds.VolumeFreeWarningPercent,
            thresholds.VolumeFreeCriticalPercent,
            cause);

        if (!volume.IsSystem) {
            return byPercent;
        }

        HealthCondition byBytes = HealthCondition.Falling(
            volume.FreeBytes / BytesPerGigabyte,
            double.NegativeInfinity,
            thresholds.SystemVolumeFreeCriticalGb,
            cause);

        return HealthCondition.Worst([byPercent, byBytes]);
    }

    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Volumes.Count == 0) {
            return new HealthReading { Resource = Resource };
        }

        (HealthVolume Volume, HealthCondition Condition)[] judged = sample.Volumes
            .Select(volume => (volume, Condition(volume, thresholds)))
            .ToArray();

        HealthCondition worst = HealthCondition.Worst(judged.Select(item => item.Condition));

        // The volume behind the status, which needn't be the fullest: a small system volume can be
        // under its limit in bytes while a larger one is fuller.
        HealthVolume shown = worst.Level >= HealthLevel.Warning
            ? judged.First(item => item.Condition == worst).Volume
            : sample.Volumes.MinBy(FreePercent)!;

        return new HealthReading {
            Resource    = Resource,
            Level       = worst.Level,
            Cause       = worst.Cause,
            Severity    = worst.Severity,
            IsImmediate = true,
            Measures    = new VolumesHealthMeasures(
                shown.Name,
                PercentPerRatio - FreePercent(shown),
                shown.FreeBytes,
                shown.FillBytesPerMinute)
        };
    }

    private static double FreePercent(HealthVolume volume) =>
        volume.TotalBytes > 0
            ? volume.FreeBytes / (double)volume.TotalBytes * PercentPerRatio
            : PercentPerRatio;

    public HealthResource Resource => HealthResource.Volumes;
}
