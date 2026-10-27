using Task.Monitor.Gui.Controls.Drivers;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Drivers;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class DriversControlTests : IDisposable
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public DriversControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
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
        DriversControl ctrl = new(
            runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private DriversControl CreateControl(int width = 160, int height = 40)
    {
        DriversControl ctrl = new(
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
    public void Detail_Pane_Follows_The_Selection_In_The_Filtered_List()
    {
        DriversControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Driver("Alpha Driver", imagePath: @"C:\a\alpha.sys", status: DriverStatus.Running),
            Driver("Bravo Driver", imagePath: @"C:\b\bravo.sys", status: DriverStatus.Stopped),
            Driver("Charlie Driver", imagePath: @"C:\c\charlie.sys", status: DriverStatus.Running)));
        ctrl.Draw();

        FilterKeys.Apply(ctrl, "charlie");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("charlie.sys", output);
        Assert.DoesNotContain("alpha.sys", output);

        ctrl.Unload();
    }

    public void Dispose() => Control.RedrawEnabled = true;

    [Fact]
    public void Draws_A_Detail_Pane_For_The_Selected_Driver()
    {
        DriversControl ctrl = CreateControl();

        const string longPath =
            @"C:\Windows\System32\DriverStore\FileRepository\acpipagr.inf_amd64_d1093347a27ff89c\acpipagr.sys";

        ctrl.Sample(SnapshotWith(
            Driver("ACPI Processor Aggregator Driver", imagePath: longPath, publisher: "Microsoft Corporation"),
            Driver("3ware")));
        ctrl.Draw();

        string output = CapturedOutput();

        // The first row is selected by default; its fields show in full in the detail pane, even
        // though the path is too long to fit in the main table's PATH column.
        foreach (string fragment in new[] {
            "FIELD", "VALUE", "Name", "Status", "Startup Type", "Version", "Publisher", "Path",
            "ACPI Processor Aggregator Driver", "Running", "Boot Start", "Microsoft Corporation", longPath
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Column_Headers_And_A_Row_Per_Driver()
    {
        DriversControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Driver("Microsoft ACPI Driver", status: DriverStatus.Running,
                startType: DriverStartType.BootStart,
                imagePath: @"C:\Windows\System32\drivers\ACPI.sys", version: "10.0.26100.9278"),
            Driver("3ware", status: DriverStatus.Stopped,
                startType: DriverStartType.ManualStart,
                imagePath: @"C:\Windows\System32\drivers\3ware.sys", version: "5.1.0.51")));
        ctrl.Draw();

        string output = CapturedOutput();

        foreach (string fragment in new[] {
            "NAME", "VERSION", "STATUS", "START TYPE", "PATH",
            "Microsoft ACPI Driver", "Running", "Boot Start", "10.0.26100.9278", "ACPI.sys",
            "3ware", "Stopped", "Manual", "5.1.0.51",
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Draws_The_Empty_Text_Before_A_Snapshot_Arrives()
    {
        DriversControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("Gathering drivers", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Kext_Status_And_Start_Type()
    {
        DriversControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Driver("IOKit USB host family", status: DriverStatus.Loaded,
                startType: DriverStartType.BootStart,
                imagePath: "/System/Library/Extensions/IOUSBHostFamily.kext", version: "1.2"),
            Driver("HighPointRR", status: DriverStatus.NotLoaded,
                startType: DriverStartType.OnDemand,
                imagePath: "/Library/Extensions/HighPointRR.kext", version: "1.1.12")));
        ctrl.Draw();

        string output = CapturedOutput();

        foreach (string fragment in new[] { "Loaded", "Not Loaded", "On Demand" }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
    }

    private static DriverInfo Driver(
        string displayName,
        string name = "Drv",
        DriverStatus status = DriverStatus.Running,
        DriverStartType startType = DriverStartType.BootStart,
        bool delayedAutoStart = false,
        string? imagePath = @"C:\Windows\System32\drivers\drv.sys",
        string? version = "10.0.26100.1150",
        string? publisher = null) =>
        new() {
            Name = name,
            DisplayName = displayName,
            Status = status,
            StartType = startType,
            DelayedAutoStart = delayedAutoStart,
            ImagePath = imagePath,
            Version = version,
            Publisher = publisher
        };

    [Fact]
    public void Escape_Clears_The_Filter()
    {
        DriversControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Driver("Alpha Driver", imagePath: @"C:\a\alpha.sys", status: DriverStatus.Running),
            Driver("Bravo Driver", imagePath: @"C:\b\bravo.sys", status: DriverStatus.Stopped),
            Driver("Charlie Driver", imagePath: @"C:\c\charlie.sys", status: DriverStatus.Running)));
        ctrl.Draw();
        FilterKeys.Apply(ctrl, "bravo");

        Assert.True(FilterKeys.Press(ctrl, ConsoleKey.Escape));

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("Alpha Driver", output);
        Assert.Contains("Charlie Driver", output);
        Assert.DoesNotContain("Filter:", output);

        ctrl.Unload();
    }

    [Fact]
    public void Filter_Shows_Only_Matching_Rows_And_The_Match_Count_In_The_Title()
    {
        DriversControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Driver("Alpha Driver", imagePath: @"C:\a\alpha.sys", status: DriverStatus.Running),
            Driver("Bravo Driver", imagePath: @"C:\b\bravo.sys", status: DriverStatus.Stopped),
            Driver("Charlie Driver", imagePath: @"C:\c\charlie.sys", status: DriverStatus.Running)));
        ctrl.Draw();

        FilterKeys.Apply(ctrl, "bravo");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("Filter: bravo (1 of 3)", output);
        Assert.Contains("Bravo Driver", output);
        Assert.DoesNotContain("Alpha Driver", output);
        Assert.DoesNotContain("Charlie Driver", output);

        ctrl.Unload();
    }

    [Fact]
    public void Filter_With_No_Matches_Shows_No_Matches_Instead_Of_Gathering()
    {
        DriversControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(Driver("Alpha Driver")));
        ctrl.Draw();

        FilterKeys.Apply(ctrl, "zzz");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        Assert.Contains("No matches", CapturedOutput());
        Assert.DoesNotContain("Gathering drivers", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Footer_Shows_The_Clear_Hint_Only_While_Filtered_And_Counts_All_Entries()
    {
        DriversControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Driver("Alpha Driver", imagePath: @"C:\a\alpha.sys", status: DriverStatus.Running),
            Driver("Bravo Driver", imagePath: @"C:\b\bravo.sys", status: DriverStatus.Stopped),
            Driver("Charlie Driver", imagePath: @"C:\c\charlie.sys", status: DriverStatus.Running)));
        ctrl.Draw();

        Assert.Contains("r Refresh     f Filter     ↑ ↓", CapturedOutput());
        Assert.DoesNotContain("Esc Clear Filter", CapturedOutput());

        FilterKeys.Apply(ctrl, "bravo");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("2 running / 3 total     r Refresh     f Filter     Esc Clear Filter     ↑ ↓", output);

        ctrl.Unload();
    }

    // Regression test: OnGotFocus redirects focus down to driversView, which makes this
    // control's own Focused flip back to false the instant that happens (see
    // Screen.FocusInternal's reentrant redirect) - HasFocus is the override that survives it.
    [Fact]
    public void HasFocus_Survives_The_OnGotFocus_Redirect()
    {
        ForwardingTerminal terminal = new(runContext.Terminal);
        Screen screen = new(terminal) { Width = 160, Height = 40 };

        DriversControl ctrl = new(runContext.ServiceController, terminal, runContext.AppConfig) {
            Width = 160,
            Height = 40
        };

        screen.Controls.Add(ctrl);
        ctrl.Load();
        ctrl.Resize();
        ctrl.SetFocus();

        Assert.False(ctrl.Focused);
        Assert.True(ctrl.HasFocus);

        ctrl.Unload();
    }

    [Fact]
    public void Ignores_A_Snapshot_With_No_Drivers_Info()
    {
        DriversControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());
        ctrl.Draw();

        Assert.Contains("Gathering drivers", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Rebuilds_The_Rows_When_The_Driver_Set_Changes()
    {
        DriversControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(Driver("Only")));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        ctrl.Sample(SnapshotWith(Driver("Only"), Driver("Added")));
        ctrl.Draw();

        Assert.Contains("Added", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void R_Without_A_Registered_DriversService_Does_Not_Throw()
    {
        DriversControl ctrl = CreateControl();
        ctrl.Draw();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('r', ConsoleKey.R, false, false, false), ref handled);

        Assert.True(handled);

        ctrl.Unload();
    }

    // A loaded kext is macOS's running driver, so it counts towards the same total.
    [Fact]
    public void Shows_Loaded_Kexts_In_The_Running_Count()
    {
        DriversControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Driver("A", status: DriverStatus.Loaded),
            Driver("B", status: DriverStatus.NotLoaded)));
        ctrl.Draw();

        Assert.Contains("1 running / 2 total", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Shows_The_Running_And_Total_Counts_In_The_Footer()
    {
        DriversControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Driver("A", status: DriverStatus.Running),
            Driver("B", status: DriverStatus.Stopped),
            Driver("C", status: DriverStatus.Running)));
        ctrl.Draw();

        Assert.Contains("2 running / 3 total", CapturedOutput());

        ctrl.Unload();
    }

    private static SystemSnapshot SnapshotWith(params DriverInfo[] drivers) =>
        new() { Drivers = new DriversInfo { Specs = new DriversSpecs { Drivers = [.. drivers] } } };

    [Fact]
    public void Updates_The_Detail_Pane_When_The_Selection_Moves()
    {
        DriversControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Driver("ACPI"),
            Driver("3ware", imagePath: @"C:\Windows\System32\drivers\3ware.sys")));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);

        Assert.Contains(@"C:\Windows\System32\drivers\3ware.sys", CapturedOutput());

        ctrl.Unload();
    }
}
