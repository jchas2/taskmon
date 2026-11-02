using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class VolumesHealthRule : IHealthRule
{
    private const double BytesPerGigabyte = 1024.0 * 1024.0 * 1024.0;
    private const double HoursPerDay      = 24.0;
    private const double MaxDaysToShow    = 30.0;
    private const double MinutesPerHour   = 60.0;
    private const double NotChangingBytes = 1024.0 * 1024.0;
    private const double PercentPerRatio  = 100.0;

    private static HealthCondition Condition(HealthVolume volume, HealthThresholds thresholds)
    {
        double freePercent = FreePercent(volume);
        string reason      = FullText(volume);

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

    private static string DescribeTimeLeft(double minutes)
    {
        if (minutes < MinutesPerHour) {
            return $"{Math.Max(1.0, minutes):0} min";
        }

        double hours = minutes / MinutesPerHour;

        return hours < HoursPerDay
            ? $"{hours:0} h"
            : $"{hours / HoursPerDay:0} days";
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
            Resource                 = Resource,
            Level                    = worst.Level,
            Reason                   = worst.Reason,
            Severity                 = worst.Severity,
            IsImmediate              = true,
            IsExplainedByUtilisation = true,
            Utilisation              = FullText(shown),
            Saturation               = TrendText(shown)
        };
    }

    private static double FreePercent(HealthVolume volume) =>
        volume.TotalBytes > 0
            ? volume.FreeBytes / (double)volume.TotalBytes * PercentPerRatio
            : PercentPerRatio;

    private static string FullText(HealthVolume volume) =>
        $"{volume.Name} {PercentPerRatio - FreePercent(volume):0}% full";

    public HealthResource Resource => HealthResource.Volumes;

    private static string TrendText(HealthVolume volume)
    {
        if (volume.FillBytesPerMinute is not { } rate) {
            return string.Empty;
        }

        if (Math.Abs(rate) < NotChangingBytes) {
            return "Not changing";
        }

        string speed = $"{((long)Math.Abs(rate)).ToFormattedByteSize()}/min";

        if (rate < 0.0) {
            return $"Freeing {speed}";
        }

        double minutesLeft = volume.FreeBytes / rate;

        return minutesLeft <= MaxDaysToShow * HoursPerDay * MinutesPerHour
            ? $"Filling {speed}, about {DescribeTimeLeft(minutesLeft)} left"
            : $"Filling {speed}";
    }
}
