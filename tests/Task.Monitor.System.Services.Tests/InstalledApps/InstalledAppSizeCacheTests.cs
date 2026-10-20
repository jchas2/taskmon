using System.Runtime.Versioning;
using Task.Monitor.System.Services.InstalledApps;

namespace Task.Monitor.System.Services.Tests.InstalledApps;

public sealed class InstalledAppSizeCacheTests : IDisposable
{
    private readonly string bundle;

    public InstalledAppSizeCacheTests()
    {
        // A fake bundle: 1,000 + 2,500 bytes, one of them in a hidden file, one level down.
        bundle = Directory.CreateTempSubdirectory("taskmon-bundle-").FullName;
        string contents = Directory.CreateDirectory(Path.Combine(bundle, "Contents")).FullName;

        File.WriteAllBytes(Path.Combine(contents, "Info.plist"), new byte[1000]);
        File.WriteAllBytes(Path.Combine(contents, ".hidden"), new byte[2500]);
    }

    [Fact]
    public void ComputePending_Leaves_Apps_Pending_When_The_Budget_Is_Spent()
    {
        InstalledAppSizeCache cache = new();

        InstalledAppsSpecs result = cache.ComputePending(PendingSpecs(), TimeSpan.Zero, CancellationToken.None);

        Assert.True(result.Apps[0].SizePending);
    }

    [Fact]
    public void ComputePending_Measures_Pending_Apps_And_Leaves_The_Rest_Alone()
    {
        InstalledAppSizeCache cache = new();
        InstalledAppsSpecs specs = PendingSpecs();

        InstalledAppsSpecs result = cache.ComputePending(specs, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.False(result.Apps[0].SizePending);
        Assert.Equal(4, result.Apps[0].EstimatedSizeKb);
        Assert.Same(specs.Apps[1], result.Apps[1]);
    }

    [Fact]
    public void ComputePending_Returns_The_Same_Specs_When_Nothing_Is_Pending()
    {
        InstalledAppsSpecs specs = new() { Apps = [new InstalledApp { Name = "Known", SizePending = false }] };

        Assert.Same(specs, new InstalledAppSizeCache().ComputePending(specs, TimeSpan.FromSeconds(30), CancellationToken.None));
    }

    [SkippableFact]
    [SupportedOSPlatform("macos")]
    public void ComputeSizeKb_Does_Not_Follow_Symbolic_Links()
    {
        // Bundles often link to shared frameworks; following the link would count them twice.
        string target = Directory.CreateTempSubdirectory("taskmon-target-").FullName;

        try {
            File.WriteAllBytes(Path.Combine(target, "large.bin"), new byte[100_000]);
            Directory.CreateSymbolicLink(Path.Combine(bundle, "Contents", "Linked"), target);

            Assert.Equal(4, InstalledAppSizeCache.ComputeSizeKb(bundle, CancellationToken.None));
        }
        finally {
            Directory.Delete(target, recursive: true);
        }
    }

    [Fact]
    public void ComputeSizeKb_Returns_Null_For_A_Missing_Path()
    {
        Assert.Null(InstalledAppSizeCache.ComputeSizeKb(Path.Combine(bundle, "missing"), CancellationToken.None));
    }

    [Fact]
    public void ComputeSizeKb_Returns_Null_When_Cancelled()
    {
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();

        Assert.Null(InstalledAppSizeCache.ComputeSizeKb(bundle, cancelled.Token));
    }

    [Fact]
    public void ComputeSizeKb_Sums_Every_File_Including_Hidden_Ones()
    {
        // 3,500 bytes round up to 4 KB.
        Assert.Equal(4, InstalledAppSizeCache.ComputeSizeKb(bundle, CancellationToken.None));
    }

    public void Dispose() => Directory.Delete(bundle, recursive: true);

    private InstalledAppsSpecs PendingSpecs() => new() {
        Apps = [
            new InstalledApp { Name = "Fake", InstallLocation = bundle, SizePending = true },
            new InstalledApp { Name = "Known", EstimatedSizeKb = 7, SizePending = false }
        ]
    };

    [Fact]
    public void TryGetCached_Forgets_A_Size_When_The_Bundle_Changes()
    {
        InstalledAppSizeCache cache = new();
        cache.ComputePending(PendingSpecs(), TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.True(cache.TryGetCached(bundle, out long? sizeKb));
        Assert.Equal(4, sizeKb);

        File.SetLastWriteTimeUtc(Path.Combine(bundle, "Contents", "Info.plist"), DateTime.UtcNow.AddMinutes(5));

        Assert.False(cache.TryGetCached(bundle, out _));
    }

    [Fact]
    public void TryGetCached_Is_False_Before_A_Bundle_Is_Measured()
    {
        Assert.False(new InstalledAppSizeCache().TryGetCached(bundle, out long? sizeKb));
        Assert.Null(sizeKb);
    }
}
