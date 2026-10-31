using System.Drawing;
using Moq;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui.Controls.DiskSpace;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.DiskSpace;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

// Control.RedrawEnabled is a process-wide static the scan-path prompt toggles off while it is
// open (the same mechanism Screen's own message/input boxes use) - IDisposable here guarantees it
// is always put back, even if a test fails before it would otherwise be restored, so one test
// opening the prompt can never silently blind every Draw() call in tests that run after it.
public sealed class DiskSpaceControlTests : IDisposable
{
    // Built from the platform's own root and separator ("C:\Root" on Windows, "/Root" on macOS), so
    // the tests exercise the same path handling as a real scan on whichever OS runs them.
    private static readonly string Root = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "Root");

    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public DiskSpaceControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private static DiskSpaceSpecs BuildScanningSpecs()
    {
        DiskSpaceAccumulator accumulator = new(Root);
        accumulator.AddFile(UnderRoot("AppData"), UnderRoot("AppData", "big.bin"), 1_000_000);
        accumulator.AddFile(UnderRoot("Documents"), UnderRoot("Documents", "small.txt"), 1_000);
        return accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 500);
    }

    // Entering folder i marks folder i-1 done (once the walk has moved on to a new root-level
    // sibling), so entering one more folder than `completed` leaves exactly `completed` done.
    private static DiskSpaceSpecs BuildSpecsWithRootProgress(int total, int completed)
    {
        DiskSpaceAccumulator accumulator = new(Root);

        for (int i = 0; i < total; i++) {
            accumulator.RegisterDiscoveredFolder(Root, UnderRoot($"Folder{i}"));
        }

        for (int i = 0; i <= completed && i < total; i++) {
            accumulator.EnterFolder(UnderRoot($"Folder{i}"));
        }

        return accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0);
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Choosing_Custom_Path_Falls_Through_To_The_Free_Text_Scan_Prompt()
    {
        DiskSpaceControl ctrl = CreateControl();
        ctrl.Draw();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('s', ConsoleKey.S, false, false, false), ref handled);

        // "Custom path..." always sits one row past the last real candidate.
        int realCandidateCount = Task.Monitor.System.Services.DiskSpace.ScanRootProvider.GetCandidates().Count;

        for (int i = 0; i < realCandidateCount; i++) {
            ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);
        }

        runContextHelper.terminal.Invocations.Clear();
        ctrl.KeyPressed(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false), ref handled);

        // ShowScanPathPrompt pre-fills the free-text box with the system drive, same as before
        // this control existed - that's the visible signal the fallback prompt actually opened.
        string defaultRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? string.Empty;
        Assert.Contains(defaultRoot, CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        DiskSpaceControl ctrl = new(
            runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private DiskSpaceControl CreateControl(int width = 160, int height = 40)
    {
        DiskSpaceControl ctrl = new(
            runContext.ServiceController,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = width,
            Height = height
        };

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    [Fact]
    public void C_Without_A_Registered_DiskSpaceService_Does_Not_Throw()
    {
        DiskSpaceControl ctrl = CreateControl();
        ctrl.Draw();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('c', ConsoleKey.C, false, false, false), ref handled);

        Assert.True(handled);

        ctrl.Unload();
    }

    public void Dispose() => Control.RedrawEnabled = true;

    [Fact]
    public void Draws_A_Row_Per_File_Sorted_By_Size_Descending()
    {
        DiskSpaceControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(BuildScanningSpecs()));
        ctrl.Draw();

        string output = CapturedOutput();

        foreach (string fragment in new[] { "FILE", "SIZE", "PATH", "big.bin", "small.txt" }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Scan_Hint_In_The_Heat_Maps_Border_Before_Any_Scan_Has_Started()
    {
        DiskSpaceControl ctrl = CreateControl();
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("Start scanning", output);
        Assert.Contains("Cancel", output);
        Assert.Contains("No files scanned yet", output);

        ctrl.Unload();
    }

    [Fact]
    public void Escape_Cancels_The_Drive_Selection_Prompt_Without_Starting_A_Scan()
    {
        DiskSpaceControl ctrl = CreateControl();
        ctrl.Draw();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('s', ConsoleKey.S, false, false, false), ref handled);
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false), ref handled);

        Assert.True(handled);

        ctrl.Unload();
    }

    [Fact]
    public void Escape_From_The_Custom_Path_Prompt_Does_Not_Start_A_Scan()
    {
        DiskSpaceControl ctrl = CreateControl();
        ctrl.Draw();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('s', ConsoleKey.S, false, false, false), ref handled);

        int realCandidateCount = Task.Monitor.System.Services.DiskSpace.ScanRootProvider.GetCandidates().Count;

        for (int i = 0; i < realCandidateCount; i++) {
            ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);
        }

        ctrl.KeyPressed(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false), ref handled);
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false), ref handled);

        Assert.True(handled);

        ctrl.Unload();
    }

    [Fact]
    public void Heat_Map_Has_No_Disk_Space_Title_Line()
    {
        DiskSpaceControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.DoesNotContain("DISK SPACE", CapturedOutput());

        ctrl.Sample(SnapshotWith(BuildScanningSpecs()));
        ctrl.Draw();

        Assert.DoesNotContain("DISK SPACE", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Ignores_A_Snapshot_With_No_DiskSpace_Info()
    {
        DiskSpaceControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());
        ctrl.Draw();

        Assert.Contains("Start scanning", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Progress_Updates_As_The_Scan_Advances()
    {
        DiskSpaceControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(BuildSpecsWithRootProgress(total: 2, completed: 0)));
        ctrl.Draw();
        Assert.Contains("0.0%", CapturedOutput());
        runContextHelper.terminal.Invocations.Clear();

        ctrl.Sample(SnapshotWith(BuildSpecsWithRootProgress(total: 2, completed: 1)));
        ctrl.Draw();
        Assert.Contains("50.0%", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Rebuilds_Rows_When_Scan_Progress_Changes()
    {
        DiskSpaceControl ctrl = CreateControl();

        DiskSpaceAccumulator accumulator = new(Root);
        accumulator.AddFile(Root, UnderRoot("one.bin"), 10);

        ctrl.Sample(SnapshotWith(accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 100)));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        accumulator.AddFile(Root, UnderRoot("two.bin"), 20);
        ctrl.Sample(SnapshotWith(accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 200)));
        ctrl.Draw();

        Assert.Contains("two.bin", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Sample_Redraws_When_The_Scan_Publishes_New_Specs()
    {
        DiskSpaceControl ctrl = CreateControl();
        DiskSpaceSpecs specs = BuildScanningSpecs();

        // Settle on one set of specs until an unchanged snapshot no longer draws.
        for (int i = 0; i < 10; i++) {
            ctrl.Sample(SnapshotWith(specs));
        }

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Sample(SnapshotWith(BuildSpecsWithRootProgress(total: 2, completed: 1)));

        Assert.Contains("50.0%", CapturedOutput());

        ctrl.Unload();
    }

    // The controller raises a snapshot every tick whether or not the scan published anything, so
    // once a scan has finished the same specs keep arriving. Redrawing them each time repainted
    // every heat map cell and flickered; they should only redraw while cells are still fading in.
    [Fact]
    public void Sample_Stops_Redrawing_Unchanged_Specs_Once_The_Heat_Map_Has_Faded_In()
    {
        DiskSpaceControl ctrl = CreateControl();
        DiskSpaceSpecs specs = BuildScanningSpecs();

        ctrl.Sample(SnapshotWith(specs));
        Assert.NotEmpty(CapturedOutput());

        // The first repeat still draws: the cells from the first draw are part way through fading in.
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Sample(SnapshotWith(specs));
        Assert.NotEmpty(CapturedOutput());

        bool stoppedRedrawing = false;

        for (int i = 0; i < 10 && !stoppedRedrawing; i++) {
            runContextHelper.terminal.Invocations.Clear();
            ctrl.Sample(SnapshotWith(specs));
            stoppedRedrawing = CapturedOutput().Length == 0;
        }

        Assert.True(stoppedRedrawing);

        ctrl.Unload();
    }

    private static DiskSpaceSpecs ScanSpecs(DiskSpaceScanState state, string? errorMessage = null)
    {
        DiskSpaceAccumulator accumulator = new(Root);
        accumulator.AddFile(UnderRoot("AppData"), UnderRoot("AppData", "big.bin"), 1_000_000);
        accumulator.AddFile(UnderRoot("Documents"), UnderRoot("Documents", "small.txt"), 1_000);
        return accumulator.Snapshot(state, elapsedMilliseconds: 1500, errorMessage);
    }

    [Fact]
    public void Shows_Full_Progress_Once_The_Scan_Completes()
    {
        DiskSpaceControl ctrl = CreateControl();

        DiskSpaceAccumulator accumulator = new(Root);
        accumulator.RegisterDiscoveredFolder(Root, UnderRoot("A"));
        accumulator.RegisterDiscoveredFolder(Root, UnderRoot("B"));
        accumulator.EnterFolder(UnderRoot("A"));
        accumulator.EnterFolder(UnderRoot("B"));
        accumulator.MarkScanComplete();

        ctrl.Sample(SnapshotWith(accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 1000)));
        ctrl.Draw();

        Assert.Contains("100.0%", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Shows_Root_Level_Folder_Completion_As_A_Percentage()
    {
        DiskSpaceControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(BuildSpecsWithRootProgress(total: 4, completed: 1)));
        ctrl.Draw();

        Assert.Contains("25.0%", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Shows_The_File_Count_In_The_Header()
    {
        DiskSpaceControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(BuildScanningSpecs()));
        ctrl.Draw();

        Assert.Contains($"LARGEST FILES (2 of top {DiskSpaceAccumulator.MaxTrackedFiles})", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Shows_The_Scan_Root_Path_As_The_Progress_Metre_Legend_Once_A_Scan_Has_One()
    {
        DiskSpaceControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(BuildScanningSpecs()));
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains(Root, output);
        Assert.DoesNotContain("Root Folders", output);

        ctrl.Unload();
    }

    [Fact]
    public void Shows_Zero_Percent_Progress_Before_Any_Scan_Has_Started()
    {
        DiskSpaceControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("Root Folders", CapturedOutput());
        Assert.Contains("0.0%", CapturedOutput());

        ctrl.Unload();
    }

    private static SystemSnapshot SnapshotWith(DiskSpaceSpecs specs) =>
        new() { DiskSpace = new DiskSpaceInfo { Specs = specs } };

    [Fact]
    public void Status_Row_Is_Empty_Before_Any_Scan_Has_Started()
    {
        DiskSpaceControl ctrl = CreateControl();
        ctrl.Draw();

        string output = CapturedOutput();

        foreach (string state in new[] { "Scanning", "Cancelling", "Completed", "Cancelled", "Failed" }) {
            Assert.DoesNotContain(state, output);
        }

        ctrl.Unload();
    }

    [Fact]
    public void Status_Row_Shows_The_Error_When_A_Faulted_Scan_Still_Has_Folders()
    {
        DiskSpaceControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(ScanSpecs(DiskSpaceScanState.Faulted, "Access denied")));
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("Failed: Access denied", output);
        Assert.Contains("AppData", output);

        ctrl.Unload();
    }

    [Theory]
    [InlineData(DiskSpaceScanState.Scanning,   "Scanning… 2 files / {0}")]
    [InlineData(DiskSpaceScanState.Cancelling, "Cancelling… 2 files / {0}")]
    [InlineData(DiskSpaceScanState.Completed,  "Completed - 2 files / {0} in 1.5s")]
    [InlineData(DiskSpaceScanState.Cancelled,  "Cancelled - 2 files / {0}")]
    public void Status_Row_Shows_The_Scan_State_Files_And_Bytes(DiskSpaceScanState state, string expected)
    {
        DiskSpaceControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(ScanSpecs(state)));
        ctrl.Draw();

        Assert.Contains(string.Format(expected, 1_001_000L.ToFormattedByteSize()), CapturedOutput());

        ctrl.Unload();
    }

    [Theory]
    [InlineData(DiskSpaceScanState.Scanning,   "Scanning… ")]
    [InlineData(DiskSpaceScanState.Cancelling, "Cancelling… ")]
    [InlineData(DiskSpaceScanState.Completed,  "Completed - ")]
    [InlineData(DiskSpaceScanState.Cancelled,  "Cancelled - ")]
    [InlineData(DiskSpaceScanState.Faulted,    "Failed: ")]
    public void Status_Row_Uses_The_State_Colour(DiskSpaceScanState state, string prefix)
    {
        DiskSpaceControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(ScanSpecs(state, "Access denied")));
        ctrl.Draw();

        Color expected = state switch {
            DiskSpaceScanState.Scanning or DiskSpaceScanState.Cancelling => runContext.AppConfig.Theme.HeatmapStateScanning,
            DiskSpaceScanState.Completed                                 => runContext.AppConfig.Theme.HeatmapStateCompleted,
            DiskSpaceScanState.Faulted                                   => runContext.AppConfig.Theme.HeatmapStateFaulted,
                                                                       _ => runContext.AppConfig.Theme.Foreground
        };

        List<IInvocation> invocations = [.. runContextHelper.terminal.Invocations];
        int statusWrite = invocations.FindIndex(invocation => invocation.Method.Name == "Write"
            && invocation.Arguments[0] is string text
            && text.StartsWith(prefix, StringComparison.Ordinal));

        Assert.True(statusWrite >= 0);

        IInvocation colourSet = invocations
            .Take(statusWrite)
            .Last(invocation => invocation.Method.Name == "set_ForegroundColor");

        Assert.Equal(expected.ToArgb(), ((Color)colourSet.Arguments[0]).ToArgb());

        ctrl.Unload();
    }

    [Fact]
    public void S_Opens_A_Drive_Selection_Prompt_Listing_The_System_Drive()
    {
        DiskSpaceControl ctrl = CreateControl();
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('s', ConsoleKey.S, false, false, false), ref handled);

        Assert.True(handled);

        string defaultRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? string.Empty;
        string output = CapturedOutput();

        Assert.Contains("Select a drive", output);
        Assert.Contains(defaultRoot, output);

        ctrl.Unload();
    }

    [Fact]
    public void The_Scan_Control_Hints_Live_On_The_Heat_Map_Border_Not_The_File_List_Footer()
    {
        DiskSpaceControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(BuildScanningSpecs()));
        ctrl.Draw();

        string output = AnsiTestHelper.StripColours(CapturedOutput());

        // The heat map's own border carries these now (see the earlier "before any scan" test) -
        // the file list's footer should be about the list itself only.
        Assert.Contains("↑ ↓ pgup pgdn Scroll", output);
        Assert.DoesNotContain("Start scanning   c Cancel   ↑ ↓ pgup pgdn Scroll", output);

        ctrl.Unload();
    }

    private static string UnderRoot(params string[] parts) => Path.Combine([Root, .. parts]);
}
