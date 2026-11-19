using System.Globalization;
using Task.Monitor.Extensions;
using Task.Monitor.Resources;

namespace Task.Monitor.Tests.Extensions;

public sealed class TimeSpanExtensionsTests
{
    [Theory]
    [InlineData(-5,   "s",   0,  0)]
    [InlineData(45,   "s",   45, 0)]
    [InlineData(720,  "min", 12, 0)]
    [InlineData(3900, "h",   1,  5)]
    public void ToDurationText_Uses_The_Coarsest_Unit_That_Still_Says_Something(int seconds, string unit, int first, int second)
    {
        string expected = unit switch {
            "s"   => string.Format(CultureInfo.CurrentCulture, Strings.HealthDurationSeconds, first),
            "min" => string.Format(CultureInfo.CurrentCulture, Strings.HealthDurationMinutes, first),
                _ => string.Format(CultureInfo.CurrentCulture, Strings.HealthDurationHoursMinutes, first, second)
        };

        Assert.Equal(expected, TimeSpan.FromSeconds(seconds).ToDurationText());
    }
}
