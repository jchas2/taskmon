using Task.Monitor.System.Services.DiskSpace;

namespace Task.Monitor.System.Services.Tests.DiskSpace;

public sealed class DiskSpaceAccumulatorTests
{
    private const string Root = @"C:\Root";

    [Fact]
    public void AddFile_Attributes_Size_To_The_Root_Level_Ancestor_Only()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.EnterFolder(Root);
        accumulator.EnterFolder(@"C:\Root\A");
        accumulator.EnterFolder(@"C:\Root\A\B");
        accumulator.AddFile(@"C:\Root\A\B", @"C:\Root\A\B\file.txt", 100);

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

        accumulator.AddFile(Root, @"C:\Root\one.txt", 10);
        accumulator.AddFile(Root, @"C:\Root\two.txt", 25);

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(35, specs.RootNode!.TotalBytes);
        Assert.Equal(35, specs.TotalBytesScanned);
        Assert.Equal(2, specs.FilesScanned);
    }

    [Fact]
    public void Snapshot_Sorts_Children_By_TotalBytes_Descending()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.AddFile(@"C:\Root\Small", @"C:\Root\Small\a.txt", 5);
        accumulator.AddFile(@"C:\Root\Large", @"C:\Root\Large\b.txt", 500);
        accumulator.AddFile(@"C:\Root\Medium", @"C:\Root\Medium\c.txt", 50);

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(["Large", "Medium", "Small"], specs.RootNode!.Children.Select(c => c.Name));
    }

    [Fact]
    public void TopFiles_Are_Sorted_By_Size_Descending()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.AddFile(Root, @"C:\Root\small.txt", 1);
        accumulator.AddFile(Root, @"C:\Root\big.txt", 1000);
        accumulator.AddFile(Root, @"C:\Root\medium.txt", 100);

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(
            [@"C:\Root\big.txt", @"C:\Root\medium.txt", @"C:\Root\small.txt"],
            specs.TopFiles.Select(f => f.FullPath));
    }

    [Fact]
    public void TopFiles_Is_Capped_At_MaxTrackedFiles()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        for (int i = 0; i < DiskSpaceAccumulator.MaxTrackedFiles + 50; i++) {
            accumulator.AddFile(Root, $@"C:\Root\file{i}.txt", i);
        }

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(DiskSpaceAccumulator.MaxTrackedFiles, specs.TopFiles.Count);

        // The smallest files (0..49) should have been evicted in favour of the largest ones.
        Assert.DoesNotContain(specs.TopFiles, f => f.FullPath == @"C:\Root\file0.txt");
        Assert.Contains(specs.TopFiles, f => f.FullPath == @"C:\Root\file249.txt");
    }

    [Fact]
    public void EnterFolder_Increments_FoldersScanned_And_Tracks_CurrentPath_Only_While_Scanning()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.EnterFolder(Root);
        accumulator.EnterFolder(@"C:\Root\A");

        DiskSpaceSpecs scanning = accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0);
        Assert.Equal(2, scanning.FoldersScanned);
        Assert.Equal(@"C:\Root\A", scanning.CurrentPath);

        DiskSpaceSpecs completed = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);
        Assert.Equal(string.Empty, completed.CurrentPath);
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
    public void EnterFolder_With_No_Files_Still_Appears_In_The_Tree_With_A_Zero_Total()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.EnterFolder(Root);
        accumulator.EnterFolder(@"C:\Root\Empty");

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        DiskSpaceFolderNode empty = Assert.Single(specs.RootNode!.Children);
        Assert.Equal("Empty", empty.Name);
        Assert.Equal(0, empty.TotalBytes);
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
    public void RegisterDiscoveredFolder_Sets_RootLevelFoldersTotal_Before_Any_Of_Them_Are_Visited()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.RegisterDiscoveredFolder(Root, @"C:\Root\A");
        accumulator.RegisterDiscoveredFolder(Root, @"C:\Root\B");

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0);

        Assert.Equal(2, specs.RootLevelFoldersTotal);
        Assert.Equal(0, specs.RootLevelFoldersCompleted);
    }

    [Fact]
    public void RegisterDiscoveredFolder_Ignores_A_Folder_Discovered_Below_The_Root_Level()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.RegisterDiscoveredFolder(Root, @"C:\Root\A");
        accumulator.RegisterDiscoveredFolder(@"C:\Root\A", @"C:\Root\A\Sub");

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0);

        Assert.Equal(1, specs.RootLevelFoldersTotal);
    }

    [Fact]
    public void EnterFolder_Marks_The_Previous_Root_Level_Folder_Completed_When_The_Walk_Moves_To_A_Sibling()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.RegisterDiscoveredFolder(Root, @"C:\Root\A");
        accumulator.RegisterDiscoveredFolder(Root, @"C:\Root\B");

        accumulator.EnterFolder(Root);
        accumulator.EnterFolder(@"C:\Root\A");
        accumulator.EnterFolder(@"C:\Root\A\Sub");

        DiskSpaceSpecs beforeSibling = accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0);
        Assert.Equal(0, beforeSibling.RootLevelFoldersCompleted);

        // The walk has finished A's whole subtree and moved on to B - A is now done.
        accumulator.EnterFolder(@"C:\Root\B");

        DiskSpaceSpecs afterSibling = accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0);
        Assert.Equal(1, afterSibling.RootLevelFoldersCompleted);
        Assert.Equal(2, afterSibling.RootLevelFoldersTotal);
    }

    [Fact]
    public void MarkScanComplete_Completes_Whichever_Root_Level_Folder_Was_Last_Visited()
    {
        DiskSpaceAccumulator accumulator = new(Root);

        accumulator.RegisterDiscoveredFolder(Root, @"C:\Root\A");
        accumulator.RegisterDiscoveredFolder(Root, @"C:\Root\B");

        accumulator.EnterFolder(Root);
        accumulator.EnterFolder(@"C:\Root\A");
        accumulator.EnterFolder(@"C:\Root\B");

        accumulator.MarkScanComplete();

        DiskSpaceSpecs specs = accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 0);

        Assert.Equal(2, specs.RootLevelFoldersCompleted);
        Assert.Equal(2, specs.RootLevelFoldersTotal);
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
}
