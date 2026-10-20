using Task.Monitor.System.Services.DiskSpace;

namespace Task.Monitor.System.Services.Tests.DiskSpace;

public sealed class DiskSpaceAccumulatorTests
{
    // Built from the platform's own root and separator ("C:\Root" on Windows, "/Root" on macOS), so
    // the tests exercise the same path handling as a real scan on whichever OS runs them.
    private static readonly string Root = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "Root");

    [Fact]
    public void AddFile_Attributes_Size_To_The_Root_Level_Ancestor_Only()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.EnterFolder(Root);
        accumulator.EnterFolder(UnderRoot("A"));
        accumulator.EnterFolder(UnderRoot("A", "B"));
        accumulator.AddFile(UnderRoot("A", "B"), UnderRoot("A", "B", "file.txt"), 100);

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(100, specs.RootNode!.TotalBytes);

        // Only the scan root's immediate children are tracked/retained - a file several levels
        // down is attributed to its root-level ancestor ("A"), and nothing deeper is kept in
        // memory for the whole scanned volume.
        DiskSpaceFolderNode a = Assert.Single(specs.RootNode.Children);
        Assert.Equal("A", a.Name);
        Assert.Equal(100, a.TotalBytes);
        Assert.Empty(a.Children);
    }

    [Fact]
    public void AddFile_Sums_Multiple_Files_In_The_Same_Folder()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.AddFile(Root, UnderRoot("one.txt"), 10);
        accumulator.AddFile(Root, UnderRoot("two.txt"), 25);

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(35, specs.RootNode!.TotalBytes);
        Assert.Equal(35, specs.TotalBytesScanned);
        Assert.Equal(2, specs.FilesScanned);
    }

    [Fact]
    public void EnterFolder_Increments_FoldersScanned_And_Tracks_CurrentPath_Only_While_Scanning()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.EnterFolder(Root);
        accumulator.EnterFolder(UnderRoot("A"));

        DiskSpaceSpecs scanning = accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0);
        Assert.Equal(2, scanning.FoldersScanned);
        Assert.Equal(UnderRoot("A"), scanning.CurrentPath);

        DiskSpaceSpecs completed = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);
        Assert.Equal(string.Empty, completed.CurrentPath);
    }

    [Fact]
    public void EnterFolder_Marks_The_Previous_Root_Level_Folder_Completed_When_The_Walk_Moves_To_A_Sibling()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.RegisterDiscoveredFolder(Root, UnderRoot("A"));
        accumulator.RegisterDiscoveredFolder(Root, UnderRoot("B"));

        accumulator.EnterFolder(Root);
        accumulator.EnterFolder(UnderRoot("A"));
        accumulator.EnterFolder(UnderRoot("A", "Sub"));

        DiskSpaceSpecs beforeSibling = accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0);
        Assert.Equal(0, beforeSibling.RootLevelFoldersCompleted);

        // The walk has finished A's whole subtree and moved on to B - A is now done.
        accumulator.EnterFolder(UnderRoot("B"));

        DiskSpaceSpecs afterSibling = accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0);
        Assert.Equal(1, afterSibling.RootLevelFoldersCompleted);
        Assert.Equal(2, afterSibling.RootLevelFoldersTotal);
    }

    [Fact]
    public void EnterFolder_On_The_Root_Itself_Does_Not_Affect_Root_Level_Progress()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.EnterFolder(Root);
        accumulator.MarkScanComplete();

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(0, specs.RootLevelFoldersTotal);
        Assert.Equal(0, specs.RootLevelFoldersCompleted);
    }

    [Fact]
    public void EnterFolder_With_No_Files_Still_Appears_In_The_Tree_With_A_Zero_Total()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.EnterFolder(Root);
        accumulator.EnterFolder(UnderRoot("Empty"));

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        DiskSpaceFolderNode empty = Assert.Single(specs.RootNode!.Children);
        Assert.Equal("Empty", empty.Name);
        Assert.Equal(0, empty.TotalBytes);
    }

    [Fact]
    public void MarkScanComplete_Completes_Whichever_Root_Level_Folder_Was_Last_Visited()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.RegisterDiscoveredFolder(Root, UnderRoot("A"));
        accumulator.RegisterDiscoveredFolder(Root, UnderRoot("B"));

        accumulator.EnterFolder(Root);
        accumulator.EnterFolder(UnderRoot("A"));
        accumulator.EnterFolder(UnderRoot("B"));

        accumulator.MarkScanComplete();

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(2, specs.RootLevelFoldersCompleted);
        Assert.Equal(2, specs.RootLevelFoldersTotal);
    }

    [Fact]
    public void RegisterDiscoveredFolder_Ignores_A_Folder_Discovered_Below_The_Root_Level()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.RegisterDiscoveredFolder(Root, UnderRoot("A"));
        accumulator.RegisterDiscoveredFolder(UnderRoot("A"), UnderRoot("A", "Sub"));

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0);

        Assert.Equal(1, specs.RootLevelFoldersTotal);
    }

    [Fact]
    public void RegisterDiscoveredFolder_Sets_RootLevelFoldersTotal_Before_Any_Of_Them_Are_Visited()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.RegisterDiscoveredFolder(Root, UnderRoot("A"));
        accumulator.RegisterDiscoveredFolder(Root, UnderRoot("B"));

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0);

        Assert.Equal(2, specs.RootLevelFoldersTotal);
        Assert.Equal(0, specs.RootLevelFoldersCompleted);
    }

    [Fact]
    public void SkipFolder_Increments_FoldersSkipped()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.SkipFolder();
        accumulator.SkipFolder();

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(2, specs.FoldersSkipped);
    }

    [Fact]
    public void Snapshot_Passes_Through_ErrorMessage()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        DiskSpaceSpecs specs = accumulator.Snapshot(
            DiskSpaceScanState.Faulted, elapsedMilliseconds: 0, errorMessage: "Access denied.");

        Assert.Equal(DiskSpaceScanState.Faulted, specs.State);
        Assert.Equal("Access denied.", specs.ErrorMessage);
    }

    [Fact]
    public void Snapshot_Sorts_Children_By_TotalBytes_Descending()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.AddFile(UnderRoot("Small"), UnderRoot("Small", "a.txt"), 5);
        accumulator.AddFile(UnderRoot("Large"), UnderRoot("Large", "b.txt"), 500);
        accumulator.AddFile(UnderRoot("Medium"), UnderRoot("Medium", "c.txt"), 50);

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(["Large", "Medium", "Small"], specs.RootNode!.Children.Select(c => c.Name));
    }

    [Fact]
    public void TopFiles_Are_Sorted_By_Size_Descending()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.AddFile(Root, UnderRoot("small.txt"), 1);
        accumulator.AddFile(Root, UnderRoot("big.txt"), 1000);
        accumulator.AddFile(Root, UnderRoot("medium.txt"), 100);

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(
            [UnderRoot("big.txt"), UnderRoot("medium.txt"), UnderRoot("small.txt")],
            specs.TopFiles.Select(f => f.FullPath));
    }

    [Fact]
    public void TopFiles_Is_Capped_At_MaxTrackedFiles()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        for (int i = 0; i < DiskSpaceAccumulator.MaxTrackedFiles + 50; i++) {
            accumulator.AddFile(Root, UnderRoot($"file{i}.txt"), i);
        }

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(DiskSpaceAccumulator.MaxTrackedFiles, specs.TopFiles.Count);

        // The smallest files (0..49) should have been evicted in favour of the largest ones.
        Assert.DoesNotContain(specs.TopFiles, f => f.FullPath == UnderRoot("file0.txt"));
        Assert.Contains(specs.TopFiles, f => f.FullPath == UnderRoot("file249.txt"));
    }

    private static string UnderRoot(params string[] parts) => Path.Combine([Root, .. parts]);
}
