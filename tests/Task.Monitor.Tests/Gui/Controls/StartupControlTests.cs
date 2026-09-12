using Task.Monitor.Gui.Controls.Startup;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Startup;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class StartupControlTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    public StartupControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    private static SystemSnapshot SnapshotWith(params StartupEntry[] entries) =>
        new() { Startup = new StartupInfo { Specs = new StartupSpecs { Entries = [.. entries] } } };

    private static StartupEntry Entry(
        string name,
        StartupEntrySource source = StartupEntrySource.RunKey,
        StartupEntryScope scope = StartupEntryScope.User,
        StartupEntryState state = StartupEntryState.Enabled,
        string? publisher = null,
        string command = "app.exe") =>
        new() {
            Name = name,
            Source = source,
            Scope = scope,
            State = state,
            Publisher = publisher,
            Command = command
        };

    private StartupControl CreateControl(int width = 160, int height = 40)
    {
        StartupControl ctrl = new(
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
        StartupControl ctrl = new(
            runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    [Fact]
    public void Draws_The_Empty_Text_Before_A_Snapshot_Arrives()
    {
        StartupControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("Gathering startup applications", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Column_Headers_And_A_Row_Per_Entry()
    {
        StartupControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Entry("OneDrive", publisher: "Microsoft Corporation", command: @"C:\Users\me\OneDrive.exe /background"),
            Entry("Steam", scope: StartupEntryScope.Machine, publisher: "Valve", command: @"C:\Program Files\Steam\steam.exe -silent"),
            Entry("OldTool", state: StartupEntryState.Disabled, source: StartupEntrySource.StartupFolder)));
        ctrl.Draw();

        string output = CapturedOutput();

        foreach (string fragment in new[] {
            "NAME", "PUBLISHER", "TYPE", "STATUS", "COMMAND",
            "OneDrive", "Microsoft Corporation", "C:\\Users\\me\\OneDrive.exe /background",
            "Steam", "Valve", "Run \u00b7 Machine",
            "OldTool", "Startup Folder \u00b7 User", "Disabled",
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Draws_A_Detail_Pane_For_The_Selected_Row()
    {
        StartupControl ctrl = CreateControl();

        const string longCommand =
            @"C:\Program Files\SomeVendor\SomeProduct\LongPathName\Application.exe --with --several --long --flags";

        ctrl.Sample(SnapshotWith(
            Entry("OneDrive", publisher: "Microsoft Corporation", command: longCommand),
            Entry("Steam", scope: StartupEntryScope.Machine, publisher: "Valve")));
        ctrl.Draw();

        string output = CapturedOutput();

        // The first row is selected by default; its fields show in full in the detail pane, even
        // though the command is too long to fit in the main table's COMMAND column.
        foreach (string fragment in new[] {
            "FIELD", "VALUE", "Name", "Publisher", "Type", "Status", "Command",
            "OneDrive", "Microsoft Corporation", "Run \u00b7 User", "Enabled", longCommand
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
    }

    [Fact]
    public void Updates_The_Detail_Pane_When_The_Selection_Moves()
    {
        StartupControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Entry("OneDrive", publisher: "Microsoft Corporation"),
            Entry("Steam", scope: StartupEntryScope.Machine, publisher: "Valve")));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);

        Assert.Contains("Valve", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Shows_The_Enabled_And_Total_Counts_In_The_Footer()
    {
        StartupControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Entry("A"),
            Entry("B", state: StartupEntryState.Disabled),
            Entry("C")));
        ctrl.Draw();

        Assert.Contains("2 enabled / 3 total", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Rebuilds_The_Rows_When_The_Entry_Set_Changes()
    {
        StartupControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(Entry("Only")));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        ctrl.Sample(SnapshotWith(Entry("Only"), Entry("Added")));
        ctrl.Draw();

        Assert.Contains("Added", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Ignores_A_Snapshot_With_No_Startup_Info()
    {
        StartupControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());
        ctrl.Draw();

        Assert.Contains("Gathering startup applications", CapturedOutput());

        ctrl.Unload();
    }
}
