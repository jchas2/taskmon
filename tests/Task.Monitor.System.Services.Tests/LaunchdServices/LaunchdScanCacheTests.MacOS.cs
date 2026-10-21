#if __APPLE__
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.LaunchdServices;

namespace Task.Monitor.System.Services.Tests.LaunchdServices;

public sealed class LaunchdScanCacheTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}");

    public LaunchdScanCacheTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void GetDefinition_Reads_The_Plist()
    {
        LaunchdScanCache cache = new();

        Assert.Equal("com.example.first", cache.GetDefinition(WritePlist("job.plist", "com.example.first"))?.Label);
    }

    [Fact]
    public void GetDefinition_Rereads_A_Plist_That_Has_Changed()
    {
        LaunchdScanCache cache = new();
        string path = WritePlist("job.plist", "com.example.first");

        cache.GetDefinition(path);

        WritePlist("job.plist", "com.example.second");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal("com.example.second", cache.GetDefinition(path)?.Label);
    }

    [Fact]
    public void GetDefinition_Reuses_The_Parse_Of_An_Unchanged_Plist()
    {
        LaunchdScanCache cache = new();
        string path = WritePlist("job.plist", "com.example.first");

        LaunchdJobDefinition? first = cache.GetDefinition(path);

        Assert.Same(first, cache.GetDefinition(path));
    }

    [Fact]
    public void GetPublisher_Reads_The_Signature()
    {
        Assert.Equal("Apple Inc.", new LaunchdScanCache().GetPublisher("/bin/ls"));
    }

    // A plist no scan has read since the last prune has been removed, so its parse mustn't be kept.
    [Fact]
    public void Prune_Drops_Plists_Not_Read_Since_The_Last_Prune()
    {
        LaunchdScanCache cache = new();
        string kept    = WritePlist("kept.plist", "com.example.kept");
        string dropped = WritePlist("dropped.plist", "com.example.dropped");

        LaunchdJobDefinition? keptFirst    = cache.GetDefinition(kept);
        LaunchdJobDefinition? droppedFirst = cache.GetDefinition(dropped);
        cache.Prune();

        cache.GetDefinition(kept);
        cache.Prune();

        Assert.Same(keptFirst, cache.GetDefinition(kept));
        Assert.NotSame(droppedFirst, cache.GetDefinition(dropped));
    }

    private string WritePlist(string fileName, string label)
    {
        string path = Path.Combine(folder, fileName);

        File.WriteAllText(path, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>{label}</string>
                <key>Program</key>
                <string>/usr/libexec/example</string>
            </dict>
            </plist>
            """);

        return path;
    }
}
#endif
