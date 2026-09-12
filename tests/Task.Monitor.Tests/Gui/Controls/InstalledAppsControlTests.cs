using Task.Monitor.Gui.Controls.InstalledApps;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.InstalledApps;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class InstalledAppsControlTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public InstalledAppsControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private static SystemSnapshot SnapshotWith(params InstalledApp[] apps) =>
        new() { InstalledApps = new InstalledAppsInfo { Specs = new InstalledAppsSpecs { Apps = [.. apps] } } };

    private static InstalledApp App(
        string name,
        string? version = "1.0.0",
        string? publisher = null,
        InstalledAppScope scope = InstalledAppScope.Machine,
        DateTime? installDate = null,
        long? estimatedSizeKb = null,
        string? installLocation = null) =>
        new() {
            Name = name,
            Version = version,
            Publisher = publisher,
            Scope = scope,
            InstallDate = installDate,
            EstimatedSizeKb = estimatedSizeKb,
            InstallLocation = installLocation
        };

    private InstalledAppsControl CreateControl(int width = 160, int height = 40)
    {
        InstalledAppsControl ctrl = new(
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
        InstalledAppsControl ctrl = new(
            runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    [Fact]
    public void Draws_The_Empty_Text_Before_A_Snapshot_Arrives()
    {
        InstalledAppsControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("Gathering installed applications", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Column_Headers_And_A_Row_Per_App()
    {
        InstalledAppsControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            App("Git", version: "2.44.0", publisher: "Git for Windows",
                installDate: new DateTime(2026, 1, 15), estimatedSizeKb: 51200,
                installLocation: @"C:\Program Files\Git"),
            App("MyTool", version: "0.9", scope: InstalledAppScope.User)));
        ctrl.Draw();

        string output = CapturedOutput();

        foreach (string fragment in new[] {
            "NAME", "VERSION", "PUBLISHER", "SCOPE", "INSTALLED", "SIZE", "LOCATION",
            "Git", "2.44.0", "Git for Windows", "Machine", "2026-01-15", "50.0 MB",
            @"C:\Program Files\Git",
            "MyTool", "0.9", "User"
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Draws_A_Detail_Pane_For_The_Selected_App()
    {
        InstalledAppsControl ctrl = CreateControl();

        const string longLocation =
            @"C:\Program Files\SomeVendor\SomeProduct\LongPathName\With\Several\Nested\Folders";

        ctrl.Sample(SnapshotWith(
            App("Git", version: "2.44.0", publisher: "Git for Windows",
                installDate: new DateTime(2026, 1, 15), estimatedSizeKb: 51200,
                installLocation: longLocation),
            App("MyTool", version: "0.9", scope: InstalledAppScope.User)));
        ctrl.Draw();

        string output = CapturedOutput();

        // The first row is selected by default; its fields show in full in the detail pane, even
        // though the install location is too long to fit in the main table's LOCATION column.
        foreach (string fragment in new[] {
            "FIELD", "VALUE", "Name", "Version", "Publisher", "Scope", "Installed", "Size", "Location",
            "Git", "2.44.0", "Git for Windows", "Machine", "2026-01-15", "50.0 MB", longLocation
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
    }

    [Fact]
    public void Updates_The_Detail_Pane_When_The_Selection_Moves()
    {
        InstalledAppsControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            App("Git", publisher: "Git for Windows"),
            App("MyTool", publisher: "Acme", scope: InstalledAppScope.User)));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);

        Assert.Contains("Acme", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Shows_The_Total_Count_In_The_Footer()
    {
        InstalledAppsControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(App("A"), App("B"), App("C")));
        ctrl.Draw();

        Assert.Contains("3 installed", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Rebuilds_The_Rows_When_The_App_Set_Changes()
    {
        InstalledAppsControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(App("Only")));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        ctrl.Sample(SnapshotWith(App("Only"), App("Added")));
        ctrl.Draw();

        Assert.Contains("Added", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Ignores_A_Snapshot_With_No_InstalledApps_Info()
    {
        InstalledAppsControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());
        ctrl.Draw();

        Assert.Contains("Gathering installed applications", CapturedOutput());

        ctrl.Unload();
    }
}
