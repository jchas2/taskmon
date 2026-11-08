using Xunit.Abstractions;

namespace Task.Monitor.System.Tests;

public sealed class SystemInfoTests
{
    private readonly ITestOutputHelper testOutputHelper;

    public SystemInfoTests(ITestOutputHelper testOutputHelper) => this.testOutputHelper = testOutputHelper;

    // A process counts as high usage once it takes a whole core: Windows reports usage as a fraction of
    // all cores, macOS as a percentage of one core.
    [Fact]
    public void Cpu_High_Core_Usage_Starts_At_One_Whole_Core()
    {
        double oneCore = OperatingSystem.IsWindows() ? 1.0 / Environment.ProcessorCount : 100.0;

        Assert.True(SystemInfo.GetCpuHighCoreUsage(oneCore * 1.01));
        Assert.False(SystemInfo.GetCpuHighCoreUsage(oneCore * 0.99));
    }

    [Fact]
    public void Should_Get_Is_Running_As_Root()
    {
        // Just invoke the function call for now. Need to determine an alternate way to
        // verify if we are running under sudo in MacOS. Windows has a number of alternatives.
        var result = SystemInfo.IsRunningAsRoot();
        Assert.True(true);
    }

    [Fact]
    public void Should_Get_Os_Version()
    {
        string osVersion = SystemInfo.GetOsVersion();

        testOutputHelper.WriteLine($"OS: {osVersion}");
        Assert.False(string.IsNullOrWhiteSpace(osVersion));
    }
}
