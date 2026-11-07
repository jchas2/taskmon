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

    private static readonly ConsoleKeyInfo Space = new(' ', ConsoleKey.Spacebar, false, false, false);

    public StartupControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    [Fact]
    public void A_Failed_Toggle_Keeps_The_Checkbox_And_Says_Why_Until_Dismissed()
    {
        StartupControl ctrl = CreateControl(canSetEnabled: true);
        List<(string Name, bool Enable)> calls = [];
        ctrl.SetEntryEnabled = RecordingToggle(calls, StartupToggleResult.AccessDenied);
        ctrl.Sample(SnapshotWith(Entry("Alpha App", scope: StartupEntryScope.Machine)));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        Assert.True(ListKeys.Press(ctrl, Space));

        Assert.False(Control.RedrawEnabled);
        Assert.Contains("Disable Startup App", CapturedOutput());
        Assert.Contains("Couldn't disable Alpha App", CapturedOutput());

        // The dialog takes every key while it is up.
        Assert.True(ListKeys.Press(ctrl, Space));
        Assert.Single(calls);

        Assert.True(ListKeys.Press(ctrl, ConsoleKey.Enter));
        Assert.True(Control.RedrawEnabled);

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        Assert.Contains("[x] Alpha App", VisibleOutput());
        Assert.DoesNotContain("Couldn't disable", VisibleOutput());

        ctrl.Unload();
    }

    [Fact]
    public void A_Toggle_Keeps_The_List_Scrolled_Where_It_Was()
    {
        StartupControl ctrl = CreateControl(canSetEnabled: true);
        List<(string Name, bool Enable)> calls = [];
        ctrl.SetEntryEnabled = RecordingToggle(calls);
        ctrl.Sample(SnapshotWith(Enumerable.Range(0, 60).Select(i => Entry($"App {i:00}")).ToArray()));
        ctrl.Draw();

        // Down to the 41st row, scrolling the list, then back up 10 rows, which doesn't scroll it.
        for (int i = 0; i < 40; i++) {
            ListKeys.Press(ctrl, ConsoleKey.DownArrow);
        }

        for (int i = 0; i < 10; i++) {
            ListKeys.Press(ctrl, ConsoleKey.UpArrow);
        }

        runContextHelper.terminal.Invocations.Clear();
        Assert.True(ListKeys.Press(ctrl, Space));

        string output = VisibleOutput();

        Assert.Equal([("App 30", false)], calls);
        Assert.Contains("[ ] App 30", output);
        Assert.Contains("App 40", output);
        Assert.DoesNotContain("App 05", output);

        ctrl.Unload();
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

    private StartupControl CreateControl(int width = 160, int height = 40, bool? canSetEnabled = null)
    {
        StartupControl ctrl = new(
            runContext.ServiceController,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = width,
            Height = height
        };

        if (canSetEnabled is { } value) {
            ctrl.CanSetEnabled = value;
        }

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
    public void Descending_Sort_Reverses_The_Rows_And_The_Selection_Stays_On_Its_Entry()
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

        // Alpha App was selected as the first row and is still selected as the last.
        Assert.Contains("NAME\u2193", output);
        Assert.True(output.IndexOf("Bravo App", StringComparison.Ordinal) < output.IndexOf("Alpha App", StringComparison.Ordinal));
        Assert.Contains("Alpha App", detail);
        Assert.DoesNotContain("Bravo App", detail);

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
    public void Draws_A_Checkbox_Per_Row_From_The_Entry_State_And_Keeps_The_Command_Column()
    {
        StartupControl ctrl = CreateControl(canSetEnabled: true);
        ctrl.Sample(SnapshotWith(
            Entry("Alpha App"),
            Entry("Bravo App", state: StartupEntryState.Disabled),
            Entry("Charlie App", state: StartupEntryState.Unknown)));
        ctrl.Draw();

        string output = VisibleOutput();

        Assert.Contains("[x] Alpha App", output);
        Assert.Contains("[ ] Bravo App", output);
        Assert.Contains("[ ] Charlie App", output);
        Assert.Contains("COMMAND", output);

        ctrl.Unload();
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
    public void Footer_Shows_The_Toggle_Hint_First_Only_Where_Entries_Can_Be_Changed()
    {
        StartupControl ctrl = CreateControl(canSetEnabled: true);
        ctrl.Sample(SnapshotWith(Entry("Alpha App")));
        ctrl.Draw();

        Assert.Contains("space Enable/Disable   r Refresh   s Sort", VisibleOutput());

        ctrl.Unload();

        ctrl = CreateControl(canSetEnabled: false);
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Sample(SnapshotWith(Entry("Alpha App")));
        ctrl.Draw();

        Assert.DoesNotContain("Enable/Disable", VisibleOutput());
        Assert.DoesNotContain("[x]", VisibleOutput());

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

    // Stands in for StartupService.SetEnabled, which updates the entry when the change takes.
    private static Func<StartupEntry, bool, StartupToggleResult> RecordingToggle(
        List<(string Name, bool Enable)> calls,
        StartupToggleResult              result = StartupToggleResult.Succeeded) =>
        (entry, enable) => {
            calls.Add((entry.Name, enable));

            if (result == StartupToggleResult.Succeeded) {
                entry.State = enable ? StartupEntryState.Enabled : StartupEntryState.Disabled;
            }

            return result;
        };

    [Fact]
    public void Selection_Stays_On_Its_Entry_When_A_Rescan_Adds_One_Above_It()
    {
        StartupControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Entry("Bravo App", command: "bravo.exe"),
            Entry("Charlie App", command: "charlie.exe")));
        ctrl.Draw();

        ListKeys.Press(ctrl, ConsoleKey.DownArrow);

        // A rescan builds new entries, so the selection is found again by the entry's identity.
        ctrl.Sample(SnapshotWith(
            Entry("Alpha App", command: "alpha.exe"),
            Entry("Bravo App", command: "bravo.exe"),
            Entry("Charlie App", command: "charlie.exe")));

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        string detail = output[output.IndexOf("DETAIL", StringComparison.Ordinal)..];

        Assert.Contains("charlie.exe", detail);

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
    public void Space_Disables_The_Selected_Entry_At_Once_And_Keeps_It_Selected()
    {
        StartupControl ctrl = CreateControl(canSetEnabled: true);
        List<(string Name, bool Enable)> calls = [];
        ctrl.SetEntryEnabled = RecordingToggle(calls);
        ctrl.Sample(SnapshotWith(
            Entry("Alpha App", command: "alpha.exe"),
            Entry("Bravo App", command: "bravo.exe"),
            Entry("Charlie App", command: "charlie.exe")));
        ctrl.Draw();

        ListKeys.Press(ctrl, ConsoleKey.DownArrow);
        runContextHelper.terminal.Invocations.Clear();

        Assert.True(ListKeys.Press(ctrl, Space));

        string output = VisibleOutput();
        string detail = output[output.IndexOf("DETAIL", StringComparison.Ordinal)..];

        Assert.Equal([("Bravo App", false)], calls);
        Assert.Contains("[x] Alpha App", output);
        Assert.Contains("[ ] Bravo App", output);
        Assert.Contains("2 enabled / 3 total", output);
        Assert.Contains("bravo.exe", detail);
        Assert.Contains("Disabled", detail);
        Assert.True(Control.RedrawEnabled);

        ctrl.Unload();
    }

    [Fact]
    public void Space_Does_Nothing_While_The_Sort_Picker_Is_Open()
    {
        StartupControl ctrl = CreateControl(canSetEnabled: true);
        List<(string Name, bool Enable)> calls = [];
        ctrl.SetEntryEnabled = RecordingToggle(calls);
        ctrl.Sample(SnapshotWith(Entry("Alpha App")));
        ctrl.Draw();

        ListKeys.Press(ctrl, ConsoleKey.S);
        Assert.True(ListKeys.Press(ctrl, Space));
        ListKeys.Press(ctrl, ConsoleKey.Escape);

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        Assert.Empty(calls);
        Assert.Contains("[x] Alpha App", VisibleOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Space_Enables_A_Disabled_Entry()
    {
        StartupControl ctrl = CreateControl(canSetEnabled: true);
        List<(string Name, bool Enable)> calls = [];
        ctrl.SetEntryEnabled = RecordingToggle(calls);
        ctrl.Sample(SnapshotWith(Entry("Alpha App", state: StartupEntryState.Disabled)));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        Assert.True(ListKeys.Press(ctrl, Space));

        Assert.Equal([("Alpha App", true)], calls);
        Assert.Contains("[x] Alpha App", VisibleOutput());
        Assert.Contains("1 enabled / 1 total", VisibleOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Space_Is_Left_Alone_Where_Entries_Cannot_Be_Changed()
    {
        StartupControl ctrl = CreateControl(canSetEnabled: false);
        List<(string Name, bool Enable)> calls = [];
        ctrl.SetEntryEnabled = RecordingToggle(calls);
        ctrl.Sample(SnapshotWith(Entry("Alpha App")));
        ctrl.Draw();

        Assert.False(ListKeys.Press(ctrl, Space));
        Assert.Empty(calls);

        ctrl.Unload();
    }

    [Fact]
    public void Space_Typed_Into_The_Filter_Is_Part_Of_The_Filter_Not_A_Toggle()
    {
        StartupControl ctrl = CreateControl(canSetEnabled: true);
        List<(string Name, bool Enable)> calls = [];
        ctrl.SetEntryEnabled = RecordingToggle(calls);
        ctrl.Sample(SnapshotWith(
            Entry("Alpha App"),
            Entry("Bravo App")));
        ctrl.Draw();

        ListKeys.Press(ctrl, ConsoleKey.F);

        foreach (char ch in "bravo app") {
            ListKeys.Press(ctrl, ch == ' ' ? Space : new ConsoleKeyInfo(ch, 0, false, false, false));
        }

        ListKeys.Press(ctrl, ConsoleKey.Enter);

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        Assert.Empty(calls);
        Assert.Contains("Filter: bravo app (1 of 2)", VisibleOutput());

        ctrl.Unload();
    }

    public static TheoryData<StartupToggleResult, bool, string> ToggleFailureData() => new() {
#if __APPLE__
        { StartupToggleResult.AccessDenied,     false, "It needs root.\nRun taskmon with sudo." },
        { StartupToggleResult.AccessDenied,     true,  "macOS refused the change." },
        { StartupToggleResult.Failed,           false, "macOS couldn't change it." },
#else
        { StartupToggleResult.AccessDenied,     false, "It needs administrator rights.\nRun taskmon as administrator." },
        { StartupToggleResult.AccessDenied,     true,  "Windows refused the change." },
        { StartupToggleResult.NotSupported,     false, $"{StartupTestSources.PrimaryText} items can't be disabled." },
        { StartupToggleResult.Failed,           false, "Windows couldn't change it." },
#endif
        { StartupToggleResult.NotFound,         false, "It no longer exists." },
        { StartupToggleResult.RequiresApproval, false, "Allow it in System Settings,\nGeneral > Login Items." }
    };

    [Fact]
    public void Toggle_Failure_Text_Cuts_A_Long_Name_To_The_Dialog_Width()
    {
        string text = StartupControl.BuildToggleFailureText(
            Entry(new string('A', 80)), enable: true, StartupToggleResult.NotFound, isElevated: false, width: 30);

        string heading = text.Split('\n')[0];

        Assert.Equal(30, heading.Length);
        Assert.StartsWith("Couldn't enable AAA", heading);
        Assert.EndsWith("…", heading);
    }

    [Theory]
    [MemberData(nameof(ToggleFailureData))]
    public void Toggle_Failure_Text_Names_The_Entry_And_The_Reason(StartupToggleResult result, bool isElevated, string reason)
    {
        string text = StartupControl.BuildToggleFailureText(Entry("Alpha App"), enable: false, result, isElevated, width: 56);

        Assert.Equal($"Couldn't disable Alpha App\n{reason}", text);
    }

    [Fact]
    public void Toggle_Failure_Text_Says_An_Entry_In_An_Unknown_State_Cannot_Be_Read()
    {
        string text = StartupControl.BuildToggleFailureText(
            Entry("Alpha App", state: StartupEntryState.Unknown), enable: true, StartupToggleResult.NotSupported, isElevated: false, width: 56);

        Assert.Equal("Couldn't enable Alpha App\nIts current state can't be read.", text);
    }

#if __APPLE__
    [Fact]
    public void Toggle_Failure_Text_Sends_Open_At_Login_Items_To_System_Settings()
    {
        string text = StartupControl.BuildToggleFailureText(
            Entry("Alpha App", StartupEntrySource.OpenAtLogin), enable: false, StartupToggleResult.NotSupported, isElevated: false, width: 56);

        Assert.Equal("Couldn't disable Alpha App\nOpen at Login items can't be disabled.\nUse System Settings > Login Items.", text);
    }
#endif

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
