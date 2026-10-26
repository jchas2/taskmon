using Task.Monitor.Configuration;
using Task.Monitor.Extensions;

namespace Task.Monitor.Tests.Extensions;

public sealed class UpdateIntervalExtensionsTests
{
    [Fact]
    public void CustomDisplayText_Shows_The_Milliseconds() =>
        Assert.Equal("Custom (2000 ms)", UpdateIntervalExtensions.CustomDisplayText(2000));

    [Theory]
    [InlineData(500,  UpdateInterval.Fast)]
    [InlineData(1000, UpdateInterval.High)]
    [InlineData(1500, UpdateInterval.Normal)]
    [InlineData(3000, UpdateInterval.Slow)]
    public void FromMilliseconds_Finds_The_Matching_Option(int milliseconds, UpdateInterval expected) =>
        Assert.Equal(expected, UpdateIntervalExtensions.FromMilliseconds(milliseconds));

    [Theory]
    [InlineData(0)]
    [InlineData(750)]
    [InlineData(2000)]
    public void FromMilliseconds_Returns_Null_For_A_Delay_That_Is_Not_An_Option(int milliseconds) =>
        Assert.Null(UpdateIntervalExtensions.FromMilliseconds(milliseconds));

    [Theory]
    [InlineData(UpdateInterval.Fast,   "Fast (500 ms)")]
    [InlineData(UpdateInterval.High,   "High (1000 ms)")]
    [InlineData(UpdateInterval.Normal, "Normal (1500 ms)")]
    [InlineData(UpdateInterval.Slow,   "Slow (3000 ms)")]
    public void ToDisplayText_Shows_The_Name_And_The_Milliseconds(UpdateInterval interval, string expected) =>
        Assert.Equal(expected, interval.ToDisplayText());
}
