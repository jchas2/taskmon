using System.Diagnostics;
using System.Runtime.Versioning;
using Task.Monitor.System.Services.InstalledApps;

namespace Task.Monitor.System.Services.Tests.InstalledApps;

[Collection(InstalledAppsServiceCollection.Name)]
public sealed class InstalledAppsServiceTests
{
    private const int MinimumDelay = 500;

    // Runs the service until the condition holds for the latest published apps, or the timeout
    // passes; returns the last apps published either way.
    private static List<InstalledApp>? RunUntil(Func<List<InstalledApp>, bool> condition, TimeSpan timeout)
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        SystemSnapshot? latest = null;

        controller.SystemSnapshotUpdated += (_, e) => latest = e.Snapshot;
        controller.AddService(() => new InstalledAppsService { Delay = MinimumDelay });
        controller.Start();

        Stopwatch elapsed = Stopwatch.StartNew();
        List<InstalledApp>? apps = null;

        while (elapsed.Elapsed < timeout) {
            Thread.Sleep(MinimumDelay);
            apps = latest?.InstalledApps?.Specs.Apps;

            if (apps is not null && condition(apps)) {
                break;
            }
        }

        controller.Stop();
        return apps;
    }

    [SkippableFact]
    [SupportedOSPlatform("macos")]
    public void Should_Find_The_Apps_That_Ship_With_macOS()
    {
        List<InstalledApp>? apps = RunUntil(apps => apps.Count > 0, TimeSpan.FromSeconds(30));

        Assert.NotNull(apps);

        // Safari lives in /Applications on every Mac, and Calculator in /System/Applications.
        InstalledApp safari = Assert.Single(apps, app => app.InstallLocation == "/Applications/Safari.app");
        Assert.Equal("Safari", safari.Name);
        Assert.Equal("Apple Inc.", safari.Publisher);
        Assert.Equal(InstalledAppScope.Machine, safari.Scope);
        Assert.False(string.IsNullOrWhiteSpace(safari.Version));
        Assert.NotNull(safari.InstallDate);

        Assert.Contains(apps, app => app.InstallLocation == "/System/Applications/Calculator.app");
    }

    [SkippableFact]
    [SupportedOSPlatform("macos")]
    public void Should_Measure_Bundle_Sizes_After_The_Scan()
    {
        // Waits for the first measured size rather than all of them: measuring every bundle walks
        // gigabytes of files, which would slow the timing-sensitive tests running alongside it.
        // InstalledAppSizeCacheTests covers the measuring itself.
        List<InstalledApp>? apps = RunUntil(
            apps => apps.Exists(app => app is { SizePending: false, EstimatedSizeKb: > 0 }),
            TimeSpan.FromSeconds(30));

        Assert.NotNull(apps);
        Assert.Contains(apps, app => app is { SizePending: false, EstimatedSizeKb: > 0 });
    }

    [Fact]
    public void Should_Publish_Installed_Apps_Through_The_Controller()
    {
        List<InstalledApp>? apps = RunUntil(apps => apps.Count > 0, TimeSpan.FromSeconds(30));

        Assert.NotNull(apps);

        // The exact contents depend on the host, but every app the scan produces must be
        // well-formed, whichever platform's source (Uninstall keys, app bundles) produced it.
        foreach (InstalledApp app in apps) {
            Assert.False(string.IsNullOrWhiteSpace(app.Name));
            Assert.True(Enum.IsDefined(app.Scope));
            Assert.False(string.IsNullOrWhiteSpace(app.Origin));
        }

        List<string> names = apps.Select(app => app.Name).ToList();
        Assert.Equal(names.Order(StringComparer.OrdinalIgnoreCase), names);
    }
}
