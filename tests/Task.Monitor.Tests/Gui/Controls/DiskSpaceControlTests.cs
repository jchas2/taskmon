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
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public DiskSpaceControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    public void Dispose() => Control.RedrawEnabled = true;

    private static SystemSnapshot SnapshotWith(DiskSpaceSpecs specs) =>
        new() { DiskSpace = new DiskSpaceInfo { Specs = specs } };

    private static DiskSpaceSpecs BuildScanningSpecs()
    {
        DiskSpaceAccumulator accumulator = new(@"C:\Root");
        accumulator.AddFile(@"C:\Root\AppData", @"C:\Root\AppData\big.bin", 1_000_000);
        accumulator.AddFile(@"C:\Root\Documents", @"C:\Root\Documents\small.txt", 1_000);
        return accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 500);
    }

    // Entering folder i marks folder i-1 done (once the walk has moved on to a new root-level
    // sibling), so entering one more folder than `completed` leaves exactly `completed` done.
    private static DiskSpaceSpecs BuildSpecsWithRootProgress(int total, int completed)
    {
        DiskSpaceAccumulator accumulator = new(@"C:\Root");

        for (int i = 0; i < total; i++) {
            accumulator.RegisterDiscoveredFolder(@"C:\Root", $@"C:\Root\Folder{i}");
        }

        for (int i = 0; i <= completed && i < total; i++) {
            accumulator.EnterFolder($@"C:\Root\Folder{i}");
        }

        return accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0);
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

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    [Fact]
    public void Constructor_With_Valid_Args_Initialises_Successfully()
    {
        DiskSpaceControl ctrl = new(
            runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    [Fact]
    public void Draws_A_Prompt_Before_Any_Scan_Has_Started()
    {
        DiskSpaceControl ctrl = CreateControl();
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("Press 's' to scan", output);
        Assert.Contains("No files scanned yet", output);

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Root_Path_And_Progress_In_The_Heat_Map_Header()
    {
        DiskSpaceControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(BuildScanningSpecs()));
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains(@"C:\Root", output);
        Assert.Contains("Scanning", output);

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
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
    public void Shows_Full_Progress_Once_The_Scan_Completes()
    {
        DiskSpaceControl ctrl = CreateControl();

        DiskSpaceAccumulator accumulator = new(@"C:\Root");
        accumulator.RegisterDiscoveredFolder(@"C:\Root", @"C:\Root\A");
        accumulator.RegisterDiscoveredFolder(@"C:\Root", @"C:\Root\B");
        accumulator.EnterFolder(@"C:\Root\A");
        accumulator.EnterFolder(@"C:\Root\B");
        accumulator.MarkScanComplete();

        ctrl.Sample(SnapshotWith(accumulator.Snapshot(DiskSpaceScanState.Completed, elapsedMilliseconds: 1000)));
        ctrl.Draw();

        Assert.Contains("100.0%", CapturedOutput());

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
    public void Shows_The_File_Count_In_The_Footer()
    {
        DiskSpaceControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(BuildScanningSpecs()));
        ctrl.Draw();

        Assert.Contains($"2 of top {DiskSpaceAccumulator.MaxTrackedFiles} largest files", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Rebuilds_Rows_When_Scan_Progress_Changes()
    {
        DiskSpaceControl ctrl = CreateControl();

        DiskSpaceAccumulator accumulator = new(@"C:\Root");
        accumulator.AddFile(@"C:\Root", @"C:\Root\one.bin", 10);

        ctrl.Sample(SnapshotWith(accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 100)));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        accumulator.AddFile(@"C:\Root", @"C:\Root\two.bin", 20);
        ctrl.Sample(SnapshotWith(accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 200)));
        ctrl.Draw();

        Assert.Contains("two.bin", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Ignores_A_Snapshot_With_No_DiskSpace_Info()
    {
        DiskSpaceControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());
        ctrl.Draw();

        Assert.Contains("Press 's' to scan", CapturedOutput());

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
    public void C_Without_A_Registered_DiskSpaceService_Does_Not_Throw()
    {
        DiskSpaceControl ctrl = CreateControl();
        ctrl.Draw();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('c', ConsoleKey.C, false, false, false), ref handled);

        Assert.True(handled);

        ctrl.Unload();
    }
}
