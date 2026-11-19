using System.Globalization;
using Task.Monitor.Resources;

namespace Task.Monitor.Extensions;

public static class TimeSpanExtensions
{
    public static string ToDurationText(this TimeSpan duration)
    {
        if (duration.TotalMinutes < 1.0) {
            return string.Format(CultureInfo.CurrentCulture, Strings.HealthDurationSeconds, Math.Max(0, (int)duration.TotalSeconds));
        }

        return duration.TotalHours < 1.0
            ? string.Format(CultureInfo.CurrentCulture, Strings.HealthDurationMinutes, (int)duration.TotalMinutes)
            : string.Format(CultureInfo.CurrentCulture, Strings.HealthDurationHoursMinutes, (int)duration.TotalHours, duration.Minutes);
    }
}
