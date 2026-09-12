using Task.Monitor.System.Services.DiskSpace;

namespace Task.Monitor.System.Services.Tests.DiskSpace;

public sealed class DiskSpaceWalkerTests : IDisposable
{
    private readonly string root;

    public DiskSpaceWalkerTests()
    {
        root = Path.Combine(Path.GetTempPath(), "DiskSpaceWalkerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        try {
            Directory.Delete(root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            // Best effort - a stray open handle on a temp dir shouldn't fail the test run.
        }
    }

    private string CreateFile(string relativePath, int sizeBytes)
    {
        string fullPath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, new byte[sizeBytes]);
        return fullPath;
    }

    [Fact]
    public void Walk_Sums_File_Sizes_Into_The_Accumulator()
    {
        CreateFile("a.bin", 100);
        CreateFile(Path.Combine("sub", "b.bin"), 250);

        DiskSpaceAccumulator accumulator = new(root);
        DiskSpaceWalker.Walk(root, accumulator, CancellationToken.None);

        var specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(350, specs.TotalBytesScanned);
        Assert.Equal(2, specs.FilesScanned);
        Assert.Equal(350, specs.RootNode!.TotalBytes);
    }

    [Fact]
    public void Walk_Builds_A_Nested_Tree_Matching_The_Real_Directory_Structure()
    {
        CreateFile(Path.Combine("Documents", "resume.docx"), 40);
        CreateFile(Path.Combine("Documents", "Work", "report.xlsx"), 60);
        CreateFile(Path.Combine("Music", "song.mp3"), 500);

        DiskSpaceAccumulator accumulator = new(root);
        DiskSpaceWalker.Walk(root, accumulator, CancellationToken.None);

        var specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        var music = specs.RootNode!.Children.Single(c => c.Name == "Music");
        Assert.Equal(500, music.TotalBytes);

        var documents = specs.RootNode.Children.Single(c => c.Name == "Documents");
        Assert.Equal(100, documents.TotalBytes);

        var work = documents.Children.Single(c => c.Name == "Work");
        Assert.Equal(60, work.TotalBytes);
    }

    [Fact]
    public void Walk_Reports_The_Largest_Files_In_TopFiles()
    {
        string big = CreateFile("big.bin", 1000);
        CreateFile("small.bin", 10);

        DiskSpaceAccumulator accumulator = new(root);
        DiskSpaceWalker.Walk(root, accumulator, CancellationToken.None);

        var specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(big, specs.TopFiles.First().FullPath);
    }

    [Fact]
    public void Walk_Reports_RootLevelFoldersTotal_And_Completes_All_Of_Them_On_A_Full_Walk()
    {
        CreateFile(Path.Combine("Documents", "resume.docx"), 40);
        CreateFile(Path.Combine("Documents", "Work", "report.xlsx"), 60);
        CreateFile(Path.Combine("Music", "song.mp3"), 500);

        DiskSpaceAccumulator accumulator = new(root);
        DiskSpaceWalker.Walk(root, accumulator, CancellationToken.None);

        // Walk itself never calls MarkScanComplete - that is the service's job once it knows the
        // whole walk finished without error - so the very last root-level folder visited isn't
        // counted as done until it does.
        DiskSpaceSpecs beforeMarkedComplete = accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0);
        Assert.Equal(2, beforeMarkedComplete.RootLevelFoldersTotal);
        Assert.Equal(1, beforeMarkedComplete.RootLevelFoldersCompleted);

        accumulator.MarkScanComplete();

        DiskSpaceSpecs completed = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);
        Assert.Equal(2, completed.RootLevelFoldersTotal);
        Assert.Equal(2, completed.RootLevelFoldersCompleted);
    }

    [Fact]
    public void Walk_Throws_OperationCanceled_When_The_Token_Is_Already_Cancelled()
    {
        CreateFile("a.bin", 1);

        DiskSpaceAccumulator accumulator = new(root);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => DiskSpaceWalker.Walk(root, accumulator, cts.Token));
    }

    [Fact]
    public void Walk_Invokes_OnFolderVisited_Once_Per_Directory()
    {
        CreateFile("a.bin", 1);
        CreateFile(Path.Combine("sub1", "b.bin"), 1);
        CreateFile(Path.Combine("sub2", "c.bin"), 1);

        DiskSpaceAccumulator accumulator = new(root);
        int visits = 0;

        DiskSpaceWalker.Walk(root, accumulator, CancellationToken.None, onFolderVisited: () => visits++);

        // The root plus sub1 and sub2.
        Assert.Equal(3, visits);
    }

    [Fact]
    public void Walk_Skips_A_Reparse_Point_Instead_Of_Following_It()
    {
        string realTarget = Path.Combine(root, "RealTarget");
        Directory.CreateDirectory(realTarget);
        File.WriteAllBytes(Path.Combine(realTarget, "inside.bin"), new byte[123]);

        string linkPath = Path.Combine(root, "LinkToSelf");

        try {
            Directory.CreateSymbolicLink(linkPath, root);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
            // Creating a directory symlink can require elevation or Developer Mode depending on
            // the host - the behaviour under test (skip, don't loop) only matters when a reparse
            // point can actually be created here.
            return;
        }

        DiskSpaceAccumulator accumulator = new(root);

        // A loop back to the scan root would hang (or eventually stack/queue-overflow) if reparse
        // points were followed; completing at all is the assertion.
        DiskSpaceWalker.Walk(root, accumulator, CancellationToken.None);

        var specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);
        Assert.Equal(123, specs.TotalBytesScanned);
    }
}
