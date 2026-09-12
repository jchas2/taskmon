using Task.Monitor.Gui.Controls.Drivers;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Drivers;
using Task.Monitor.System.Services.Process;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class DriversControlTests
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

    private static SystemSnapshot SnapshotWith(params DriverInfo[] drivers) =>
        new() { Drivers = new DriversInfo { Specs = new DriversSpecs { Drivers = [.. drivers] } } };

    private static DriverInfo Driver(
        string displayName,
        string serviceName = "Drv",
        WindowsServiceStatus status = WindowsServiceStatus.Running,
        WindowsServiceStartType startType = WindowsServiceStartType.BootStart,
        bool delayedAutoStart = false,
        string? imagePath = @"C:\Windows\System32\drivers\drv.sys",
        string? version = "10.0.26100.1150") =>
        new() {
            ServiceName = serviceName,
            DisplayName = displayName,
            Status = status,
            StartType = startType,
            DelayedAutoStart = delayedAutoStart,
            ImagePath = imagePath,
            Version = version
        };

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

    [Fact]
    public void Draws_The_Empty_Text_Before_A_Snapshot_Arrives()
    {
        DriversControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("Gathering drivers", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Column_Headers_And_A_Row_Per_Driver()
    {
        DriversControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Driver("Microsoft ACPI Driver", status: WindowsServiceStatus.Running,
                startType: WindowsServiceStartType.BootStart,
                imagePath: @"C:\Windows\System32\drivers\ACPI.sys", version: "10.0.26100.9278"),
            Driver("3ware", status: WindowsServiceStatus.Stopped,
                startType: WindowsServiceStartType.ManualStart,
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
    public void Draws_A_Detail_Pane_For_The_Selected_Driver()
    {
        DriversControl ctrl = CreateControl();

        const string longPath =
            @"C:\Windows\System32\DriverStore\FileRepository\acpipagr.inf_amd64_d1093347a27ff89c\acpipagr.sys";

        ctrl.Sample(SnapshotWith(
            Driver("ACPI Processor Aggregator Driver", imagePath: longPath),
            Driver("3ware")));
        ctrl.Draw();

        string output = CapturedOutput();

        // The first row is selected by default; its fields show in full in the detail pane, even
        // though the path is too long to fit in the main table's PATH column.
        foreach (string fragment in new[] {
            "FIELD", "VALUE", "Name", "Status", "Startup Type", "Version", "Path",
            "ACPI Processor Aggregator Driver", "Running", "Boot Start", longPath
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
    }

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

    [Fact]
    public void Shows_The_Running_And_Total_Counts_In_The_Footer()
    {
        DriversControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Driver("A", status: WindowsServiceStatus.Running),
            Driver("B", status: WindowsServiceStatus.Stopped),
            Driver("C", status: WindowsServiceStatus.Running)));
        ctrl.Draw();

        Assert.Contains("2 running / 3 total", CapturedOutput());

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
    public void Ignores_A_Snapshot_With_No_Drivers_Info()
    {
        DriversControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());
        ctrl.Draw();

        Assert.Contains("Gathering drivers", CapturedOutput());

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
}
