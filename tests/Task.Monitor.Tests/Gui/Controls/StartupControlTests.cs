using Task.Monitor.Gui.Controls.Startup;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Startup;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class StartupControlTests : IDisposable
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

    [Fact]
    public void Default_Sort_Is_By_Type_Like_The_Service_Order()
    {
        StartupControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(Entry("Alpha App")));
        ctrl.Draw();

        Assert.Contains("TYPE\u2191", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Descending_Sort_Reverses_The_Rows_And_The_Detail_Pane_Follows_The_Selection()
    {
        StartupControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Entry("Alpha App"),
            Entry("Bravo App")));
        ctrl.Draw();

        ListKeys.Press(ctrl, ConsoleKey.S);
        ListKeys.Press(ctrl, ConsoleKey.UpArrow);
        ListKeys.Press(ctrl, ConsoleKey.UpArrow);
        ListKeys.Press(ctrl, ConsoleKey.Enter);

        Assert.True(ListKeys.Press(ctrl, ConsoleKey.D));

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        string detail = output[output.IndexOf("DETAIL", StringComparison.Ordinal)..];

        Assert.Contains("NAME\u2193", output);
        Assert.True(output.IndexOf("Bravo App", StringComparison.Ordinal) < output.IndexOf("Alpha App", StringComparison.Ordinal));
        Assert.Contains("Bravo App", detail);
        Assert.DoesNotContain("Alpha App", detail);

        ctrl.Unload();
    }

    [Fact]
    public void Detail_Pane_Follows_The_Selection_In_The_Filtered_List()
    {
        StartupControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Entry("Alpha App", command: "alpha.exe"),
            Entry("Bravo App", state: StartupEntryState.Disabled, command: "bravo.exe"),
            Entry("Charlie App", command: "charlie.exe")));
        ctrl.Draw();

        ListKeys.ApplyFilter(ctrl, "charlie");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("charlie.exe", output);
        Assert.DoesNotContain("alpha.exe", output);

        ctrl.Unload();
    }

    public void Dispose() => Control.RedrawEnabled = true;

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
            "FIELD", "VALUE", "Name", "Publisher", "Type", "Scope", "Status", "Command",
            "OneDrive", "Microsoft Corporation", StartupTestSources.PrimaryText, "User", "Enabled", longCommand
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Column_Headers_And_A_Row_Per_Entry()
    {
        StartupControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Entry("OneDrive", publisher: "Microsoft Corporation", command: @"C:\Users\me\OneDrive.exe /background"),
            Entry("Steam", scope: StartupEntryScope.Machine, publisher: "Valve", command: @"C:\Program Files\Steam\steam.exe -silent"),
            Entry("OldTool", state: StartupEntryState.Disabled, source: StartupTestSources.Secondary)));
        ctrl.Draw();

        string output = CapturedOutput();

        foreach (string fragment in new[] {
            "NAME", "PUBLISHER", "TYPE", "SCOPE", "STATUS", "COMMAND",
            "OneDrive", "Microsoft Corporation", "C:\\Users\\me\\OneDrive.exe /background",
            "Steam", "Valve", StartupTestSources.PrimaryText, "Machine",
            "OldTool", StartupTestSources.SecondaryText, "User", "Disabled",
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Draws_The_Empty_Text_Before_A_Snapshot_Arrives()
    {
        StartupControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("Gathering startup applications", CapturedOutput());

        ctrl.Unload();
    }

    private static StartupEntry Entry(
        string name,
        StartupEntrySource source = StartupTestSources.Primary,
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

    [Fact]
    public void Escape_Clears_The_Filter()
    {
        StartupControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Entry("Alpha App", command: "alpha.exe"),
            Entry("Bravo App", state: StartupEntryState.Disabled, command: "bravo.exe"),
            Entry("Charlie App", command: "charlie.exe")));
        ctrl.Draw();
        ListKeys.ApplyFilter(ctrl, "bravo");

        Assert.True(ListKeys.Press(ctrl, ConsoleKey.Escape));

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("Alpha App", output);
        Assert.Contains("Charlie App", output);
        Assert.DoesNotContain("Filter:", output);

        ctrl.Unload();
    }

    [Fact]
    public void Filter_Matches_The_Type_And_Scope_Text()
    {
        StartupControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Entry("Alpha App", source: StartupTestSources.Primary, scope: StartupEntryScope.User),
            Entry("Bravo App", source: StartupTestSources.Secondary, scope: StartupEntryScope.Machine)));
        ctrl.Draw();

        ListKeys.ApplyFilter(ctrl, StartupTestSources.SecondaryText);
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        Assert.Contains("(1 of 2)", CapturedOutput());
        Assert.DoesNotContain("Alpha App", CapturedOutput());

        ListKeys.Press(ctrl, ConsoleKey.Escape);
        ListKeys.ApplyFilter(ctrl, "machine");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        Assert.Contains("Bravo App", CapturedOutput());
        Assert.DoesNotContain("Alpha App", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Filter_Shows_Only_Matching_Rows_And_The_Match_Count_In_The_Title()
    {
        StartupControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Entry("Alpha App", command: "alpha.exe"),
            Entry("Bravo App", state: StartupEntryState.Disabled, command: "bravo.exe"),
            Entry("Charlie App", command: "charlie.exe")));
        ctrl.Draw();

        ListKeys.ApplyFilter(ctrl, "bravo");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("Filter: bravo (1 of 3)", output);
        Assert.Contains("Bravo App", output);
        Assert.DoesNotContain("Alpha App", output);
        Assert.DoesNotContain("Charlie App", output);

        ctrl.Unload();
    }

    [Fact]
    public void Footer_Shows_The_Clear_Hint_Only_While_Filtered_And_Counts_All_Entries()
    {
        StartupControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Entry("Alpha App", command: "alpha.exe"),
            Entry("Bravo App", state: StartupEntryState.Disabled, command: "bravo.exe"),
            Entry("Charlie App", command: "charlie.exe")));
        ctrl.Draw();

        Assert.Contains("r Refresh   s Sort   f Filter   ↑ ↓", VisibleOutput());
        Assert.DoesNotContain("esc Clear Filter", VisibleOutput());

        ListKeys.ApplyFilter(ctrl, "bravo");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = VisibleOutput();
        Assert.Contains("2 enabled / 3 total", output);
        Assert.Contains("r Refresh   s Sort   f Filter   esc Clear Filter   ↑ ↓", output);

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
    public void Shows_The_Enabled_And_Total_Counts_In_The_Header()
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

    private static SystemSnapshot SnapshotWith(params StartupEntry[] entries) =>
        new() { Startup = new StartupInfo { Specs = new StartupSpecs { Entries = [.. entries] } } };

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

    private string VisibleOutput() => AnsiTestHelper.StripColours(CapturedOutput());
}
