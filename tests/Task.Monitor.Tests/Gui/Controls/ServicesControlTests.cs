using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui.Controls.Services;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.WindowsServices;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;
using Xunit.Abstractions;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ServicesControlTests : IDisposable
{
    private readonly ITestOutputHelper outputHelper;
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext runContext;

    // Records each request, as WindowsServicesService.RequestControl would take it, and keeps its
    // completion so a test can finish it as the worker would.
    private sealed class RecordedRequests
    {
        public readonly List<(string ServiceName, bool Start, Action<ServiceControlOutcome> Completed)> All = [];

        public void Add(WindowsServiceInfo service, bool start, Action<ServiceControlOutcome> completed) =>
            All.Add((service.ServiceName, start, completed));

        public IEnumerable<(string, bool)> Calls => All.Select(request => (request.ServiceName, request.Start));

        public void Complete(ServiceControlResult result, int index = 0) =>
            All[index].Completed(new ServiceControlOutcome(result));
    }

    private static readonly ConsoleKeyInfo Space = new(' ', ConsoleKey.Spacebar, false, false, false);

    public ServicesControlTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        runContextHelper = new RunContextHelper();
        runContext = runContextHelper.GetRunContext();
    }

    [Fact]
    public void A_Failure_Says_Why_And_Waits_For_Enter()
    {
        RecordedRequests requests = new();
        ServicesControl  ctrl     = CreateFocusedControl(requests, Service("Print Spooler"));

        ListKeys.Press(ctrl, Space);
        requests.Complete(ServiceControlResult.Disabled);

        Assert.False(Control.RedrawEnabled);
        Assert.Contains("Stop Service", CapturedOutput());
        Assert.Contains("Couldn't stop Print Spooler", CapturedOutput());

        // The dialog takes every key while it is up.
        Assert.True(ListKeys.Press(ctrl, Space));
        Assert.Single(requests.All);

        Assert.True(ListKeys.Press(ctrl, ConsoleKey.Enter));
        Assert.True(Control.RedrawEnabled);

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        Assert.Contains("[x] Print Spooler", VisibleOutput());
        Assert.Contains("Running", VisibleOutput());

        ctrl.Unload();
    }

    [Fact]
    public void A_Failure_Waits_Until_The_View_Has_The_Keys()
    {
        RecordedRequests requests = new();
        ServicesControl  ctrl     = CreateFocusedControl(requests, Service("Print Spooler"));

        ListKeys.Press(ctrl, Space);

        // The keys move on, to the menu or another pane, before the service settles.
        ctrl.ServicesView.Focused = false;
        requests.Complete(ServiceControlResult.StoppedAfterStarting);

        Assert.True(Control.RedrawEnabled);
        Assert.DoesNotContain("Couldn't", CapturedOutput());

        ctrl.ServicesView.Focused = true;
        ctrl.Draw();

        Assert.False(Control.RedrawEnabled);
        Assert.Contains("It started and then stopped.", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void A_Result_After_The_View_Is_Unloaded_Is_Dropped()
    {
        RecordedRequests requests = new();
        ServicesControl  ctrl     = CreateFocusedControl(requests, Service("Print Spooler"));

        ListKeys.Press(ctrl, Space);
        ctrl.Unload();
        runContextHelper.terminal.Invocations.Clear();

        requests.Complete(ServiceControlResult.AccessDenied);

        Assert.True(Control.RedrawEnabled);
        Assert.DoesNotContain("Couldn't", CapturedOutput());
    }

    [Fact]
    public void A_Running_Service_Shows_Its_Status_In_The_Low_Range_Colour()
    {
        runContext.AppConfig.Theme.RangeLowBackground = ConsolePalette.Magenta;

        ServicesControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(Service("Print Spooler", status: WindowsServiceStatus.Running)));

        Assert.Equal(ConsolePalette.Magenta, ListPaneTestHelper.SelectedStatusCell(ctrl).ForegroundColor);

        ctrl.Unload();
    }

    [Fact]
    public void A_Status_Change_Keeps_The_List_Scrolled_And_The_Service_Selected()
    {
        RecordedRequests     requests = new();
        WindowsServiceInfo[] services = Enumerable.Range(0, 60).Select(i => Service($"Service {i:00}")).ToArray();
        ServicesControl      ctrl     = CreateFocusedControl(requests, services);

        // Down to the 41st row, scrolling the list, then back up 10 rows, which doesn't scroll it.
        for (int i = 0; i < 40; i++) {
            ListKeys.Press(ctrl, ConsoleKey.DownArrow);
        }

        for (int i = 0; i < 10; i++) {
            ListKeys.Press(ctrl, ConsoleKey.UpArrow);
        }

        runContextHelper.terminal.Invocations.Clear();
        ListKeys.Press(ctrl, Space);

        services[30].Status = WindowsServiceStatus.Stopped;
        requests.Complete(ServiceControlResult.Succeeded);

        string output = VisibleOutput();
        string detail = output[output.LastIndexOf("DETAIL", StringComparison.Ordinal)..];

        Assert.Equal([("Service30", false)], requests.Calls);
        Assert.Contains("[ ] Service 30", output);
        Assert.Contains("Service 40", output);
        Assert.DoesNotContain("Service 05", output);
        Assert.Contains("Service 30", detail);
        Assert.Contains("Stopped", detail);

        ctrl.Unload();
    }

    // Regression test: a stopped service's STATUS was drawn in chart.range.mid.foreground - the dark
    // text that goes on the mid colour block - so "Stopped" showed black on the list background.
    [Fact]
    public void A_Stopped_Service_Shows_Its_Status_In_The_Mid_Range_Colour()
    {
        ServicesControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(Service("Print Spooler", status: WindowsServiceStatus.Stopped)));

        Assert.Equal(
            runContext.AppConfig.Theme.RangeMidBackground,
            ListPaneTestHelper.SelectedStatusCell(ctrl).ForegroundColor);

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
        ServicesControl ctrl = new(
            runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private ServicesControl CreateControl(int width = 160, int height = 40, bool? canControl = null)
    {
        ServicesControl ctrl = new(
            runContext.ServiceController,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width = width,
            Height = height
        };

        if (canControl is { } value) {
            ctrl.CanControl = value;
        }

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    private ServicesControl CreateFocusedControl(RecordedRequests requests, params WindowsServiceInfo[] services)
    {
        ServicesControl ctrl = CreateControl(canControl: true);
        ctrl.RequestControl = requests.Add;
        ctrl.ServicesView.Focused = true;
        ctrl.Sample(SnapshotWith(services));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();
        return ctrl;
    }

    [Fact]
    public void Descending_Sort_Reverses_The_Rows_And_The_Selection_Stays_On_Its_Service()
    {
        ServicesControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Service("Alpha Service"),
            Service("Bravo Service")));
        ctrl.Draw();

        Assert.True(ListKeys.Press(ctrl, ConsoleKey.D));

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        string detail = output[output.IndexOf("DETAIL", StringComparison.Ordinal)..];

        // Alpha Service was selected as the first row and is still selected as the last.
        Assert.Contains("NAME\u2193", output);
        Assert.True(output.IndexOf("Bravo Service", StringComparison.Ordinal) < output.IndexOf("Alpha Service", StringComparison.Ordinal));
        Assert.Contains("Alpha Service", detail);
        Assert.DoesNotContain("Bravo Service", detail);

        ctrl.Unload();
    }

    // The Service Control Manager is inconsistent about casing for these well-known accounts - real
    // services on the same machine have returned both "NT AUTHORITY\LocalService" and
    // "NT Authority\LocalService" - so the friendly name must not depend on exact casing.
    [Fact]
    public void Describes_A_Well_Known_LogOnAs_Account_Regardless_Of_Casing()
    {
        ServicesControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(Service("Application Identity", logOnAs: @"NT Authority\LocalService")));
        ctrl.Draw();

        string output = CapturedOutput();

        Assert.Contains("Local Service", output);
        Assert.DoesNotContain(@"NT Authority\LocalService", output);

        ctrl.Unload();
    }

    [Fact]
    public void Detail_Pane_Follows_The_Selection_In_The_Filtered_List()
    {
        ServicesControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Service("Alpha Service", description: "Alpha description."),
            Service("Bravo Service", status: WindowsServiceStatus.Stopped, description: "Bravo description."),
            Service("Charlie Service", description: "Charlie description.")));
        ctrl.Draw();

        ListKeys.ApplyFilter(ctrl, "charlie");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("Charlie description.", output);
        Assert.DoesNotContain("Alpha description.", output);

        ctrl.Unload();
    }

    // DETAIL lists field / value rows like the other property lists: fields in property.key and
    // values in property.value, except the status, which keeps its running / stopped highlight.
    [Fact]
    public void Detail_Rows_Use_The_Property_Colours()
    {
        runContext.AppConfig.Theme.PropertyKey        = ConsolePalette.Cyan;
        runContext.AppConfig.Theme.PropertyValue      = ConsolePalette.Yellow;
        runContext.AppConfig.Theme.RangeLowBackground = ConsolePalette.Magenta;
        runContext.AppConfig.Theme.RangeMidBackground = ConsolePalette.Red;

        ServicesControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(Service("Print Spooler")));

        ListView detail = ListPaneTestHelper.Detail(ctrl);

        Assert.NotEmpty(detail.Items);
        Assert.All(detail.Items.Cast<ListViewItem>(), row => {
            Assert.Equal(ConsolePalette.Cyan, row.SubItems[0].ForegroundColor);
            Assert.Equal(
                row.SubItems[0].Text == "Status" ? ConsolePalette.Magenta : ConsolePalette.Yellow,
                row.SubItems[1].ForegroundColor);
        });

        ctrl.Sample(SnapshotWith(Service("Print Spooler", status: WindowsServiceStatus.Stopped)));

        ListViewItem status = detail.Items.Cast<ListViewItem>().Single(row => row.SubItems[0].Text == "Status");
        Assert.Equal(ConsolePalette.Cyan, status.SubItems[0].ForegroundColor);
        Assert.Equal(ConsolePalette.Red,  status.SubItems[1].ForegroundColor);

        ctrl.Unload();
    }

    public void Dispose() => Control.RedrawEnabled = true;

    [Fact]
    public void Draws_A_Checkbox_Ticked_For_A_Service_Running_Or_On_Its_Way_Up()
    {
        ServicesControl ctrl = CreateControl(canControl: true);
        ctrl.Sample(SnapshotWith(
            Service("Running Service"),
            Service("Start Service",    status: WindowsServiceStatus.StartPending),
            Service("Paused Service",   status: WindowsServiceStatus.Paused),
            Service("Continue Service", status: WindowsServiceStatus.ContinuePending),
            Service("Stop Service",     status: WindowsServiceStatus.StopPending),
            Service("Stopped Service",  status: WindowsServiceStatus.Stopped)));
        ctrl.Draw();

        string output = VisibleOutput();

        Assert.Contains("[x] Running Service", output);
        Assert.Contains("[x] Start Service", output);
        Assert.Contains("[x] Paused Service", output);
        Assert.Contains("[x] Continue Service", output);
        Assert.Contains("[ ] Stop Service", output);
        Assert.Contains("[ ] Stopped Service", output);
        Assert.Contains("DESCRIPTION", output);

        ctrl.Unload();
    }

    [Fact]
    public void Draws_A_Detail_Pane_For_The_Selected_Service()
    {
        ServicesControl ctrl = CreateControl();

        const string longDescription =
            "This service does a great many things across a great many subsystems, described here in full.";

        ctrl.Sample(SnapshotWith(
            Service("Windows Update", description: longDescription),
            Service("Xbox Live Auth Manager")));
        ctrl.Draw();

        string output = CapturedOutput();

        // The first row is selected by default; its fields show in full in the detail pane, even
        // though the description is too long to fit in the main table's DESCRIPTION column.
        foreach (string fragment in new[] {
            "FIELD", "VALUE", "Name", "Status", "Startup Type", "Log On As", "Description",
            "Windows Update", "Running", "Automatic", "Local System", longDescription
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Column_Headers_And_A_Row_Per_Service()
    {
        ServicesControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Service("Windows Update", status: WindowsServiceStatus.Running,
                startType: WindowsServiceStartType.AutomaticStart, delayedAutoStart: true,
                logOnAs: "LocalSystem", description: "Enables detection of updates."),
            Service("Fax", status: WindowsServiceStatus.Stopped,
                startType: WindowsServiceStartType.ManualStart,
                logOnAs: @"NT AUTHORITY\NetworkService")));
        ctrl.Draw();

        string output = CapturedOutput();

        foreach (string fragment in new[] {
            "NAME", "STATUS", "STARTUP TYPE", "LOG ON AS", "DESCRIPTION",
            "Windows Update", "Running", "Automatic (Delayed Start)", "Local System",
            "Enables detection of updates.",
            "Fax", "Stopped", "Manual", "Network Service",
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
        MockInvocationsHelper.WriteInvocations(runContextHelper.terminal.Invocations, outputHelper);
    }

    [Fact]
    public void Draws_The_Empty_Text_Before_A_Snapshot_Arrives()
    {
        ServicesControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("Gathering Windows services", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Escape_Clears_The_Filter()
    {
        ServicesControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Service("Alpha Service", description: "Alpha description."),
            Service("Bravo Service", status: WindowsServiceStatus.Stopped, description: "Bravo description."),
            Service("Charlie Service", description: "Charlie description.")));
        ctrl.Draw();
        ListKeys.ApplyFilter(ctrl, "bravo");

        Assert.True(ListKeys.Press(ctrl, ConsoleKey.Escape));

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("Alpha Service", output);
        Assert.Contains("Charlie Service", output);
        Assert.DoesNotContain("Filter:", output);

        ctrl.Unload();
    }

    [Fact]
    public void Filter_Shows_Only_Matching_Rows_And_The_Match_Count_In_The_Title()
    {
        ServicesControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Service("Alpha Service", description: "Alpha description."),
            Service("Bravo Service", status: WindowsServiceStatus.Stopped, description: "Bravo description."),
            Service("Charlie Service", description: "Charlie description.")));
        ctrl.Draw();

        ListKeys.ApplyFilter(ctrl, "bravo");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("Filter: bravo (1 of 3)", output);
        Assert.Contains("Bravo Service", output);
        Assert.DoesNotContain("Alpha Service", output);
        Assert.DoesNotContain("Charlie Service", output);

        ctrl.Unload();
    }

    [Fact]
    public void Footer_Shows_The_Clear_Hint_Only_While_Filtered_And_Counts_All_Entries()
    {
        ServicesControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Service("Alpha Service", description: "Alpha description."),
            Service("Bravo Service", status: WindowsServiceStatus.Stopped, description: "Bravo description."),
            Service("Charlie Service", description: "Charlie description.")));
        ctrl.Draw();

        Assert.Contains("r Refresh   s Sort   f Filter   ↑ ↓", VisibleOutput());
        Assert.DoesNotContain("esc Clear Filter", VisibleOutput());

        ListKeys.ApplyFilter(ctrl, "bravo");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = VisibleOutput();
        Assert.Contains("2 running / 3 total", output);
        Assert.Contains("r Refresh   s Sort   f Filter   esc Clear Filter   ↑ ↓", output);

        ctrl.Unload();
    }

    [Fact]
    public void Footer_Shows_The_Start_Stop_Hint_First()
    {
        ServicesControl ctrl = CreateControl(canControl: true);
        ctrl.Sample(SnapshotWith(Service("Print Spooler")));
        ctrl.Draw();

        Assert.Contains("space Start/Stop   r Refresh   s Sort", VisibleOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Ignores_A_Snapshot_With_No_WindowsServices_Info()
    {
        ServicesControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());
        ctrl.Draw();

        Assert.Contains("Gathering Windows services", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Rebuilds_The_Rows_When_The_Service_Set_Changes()
    {
        ServicesControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(Service("Only")));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        ctrl.Sample(SnapshotWith(Service("Only"), Service("Added")));
        ctrl.Draw();

        Assert.Contains("Added", CapturedOutput());

        ctrl.Unload();
    }

    // Service names are unique, as Windows requires; by default one is made from the display name.
    private static WindowsServiceInfo Service(
        string displayName,
        string? serviceName = null,
        WindowsServiceStatus status = WindowsServiceStatus.Running,
        WindowsServiceStartType startType = WindowsServiceStartType.AutomaticStart,
        bool delayedAutoStart = false,
        string? logOnAs = "LocalSystem",
        string? description = "A service.") =>
        new() {
            ServiceName = serviceName ?? displayName.Replace(" ", string.Empty),
            DisplayName = displayName,
            Status = status,
            StartType = startType,
            DelayedAutoStart = delayedAutoStart,
            LogOnAs = logOnAs,
            Description = description
        };

    [Fact]
    public void Shows_The_Running_And_Total_Counts_In_The_Header()
    {
        ServicesControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Service("A", status: WindowsServiceStatus.Running),
            Service("B", status: WindowsServiceStatus.Stopped),
            Service("C", status: WindowsServiceStatus.Running)));
        ctrl.Draw();

        Assert.Contains("2 running / 3 total", CapturedOutput());

        ctrl.Unload();
    }

    private static SystemSnapshot SnapshotWith(params WindowsServiceInfo[] services) =>
        new() { WindowsServices = new WindowsServicesInfo { Specs = new WindowsServicesSpecs { Services = [.. services] } } };

    [Fact]
    public void Space_Does_Nothing_While_The_Sort_Picker_Is_Open()
    {
        RecordedRequests requests = new();
        ServicesControl  ctrl     = CreateFocusedControl(requests, Service("Print Spooler"));

        ListKeys.Press(ctrl, ConsoleKey.S);
        Assert.True(ListKeys.Press(ctrl, Space));
        ListKeys.Press(ctrl, ConsoleKey.Escape);

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        Assert.Empty(requests.All);
        Assert.Contains("[x] Print Spooler", VisibleOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Space_Is_Ignored_While_A_Request_Is_In_Flight_Or_The_Service_Is_Pending()
    {
        RecordedRequests requests = new();
        ServicesControl  ctrl     = CreateFocusedControl(
            requests,
            Service("Alpha Service"),
            Service("Bravo Service", status: WindowsServiceStatus.StartPending));

        Assert.True(ListKeys.Press(ctrl, Space));
        Assert.True(ListKeys.Press(ctrl, Space));

        ListKeys.Press(ctrl, ConsoleKey.DownArrow);
        Assert.True(ListKeys.Press(ctrl, Space));

        Assert.Equal([("AlphaService", false)], requests.Calls);

        ctrl.Unload();
    }

    [Fact]
    public void Space_Is_Left_Alone_Where_Services_Cannot_Be_Controlled()
    {
        RecordedRequests requests = new();
        ServicesControl  ctrl     = CreateControl(canControl: false);
        ctrl.RequestControl = requests.Add;
        ctrl.Sample(SnapshotWith(Service("Print Spooler")));
        ctrl.Draw();

        Assert.False(ListKeys.Press(ctrl, Space));
        Assert.Empty(requests.All);
        Assert.DoesNotContain("[x]", VisibleOutput());
        Assert.DoesNotContain("Start/Stop", VisibleOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Space_Starts_A_Stopped_Service_And_Shows_It_Starting()
    {
        RecordedRequests requests = new();
        ServicesControl  ctrl     = CreateFocusedControl(requests, Service("Print Spooler", status: WindowsServiceStatus.Stopped));

        Assert.Contains("[ ] Print Spooler", VisibleOutputAfterDraw(ctrl));

        runContextHelper.terminal.Invocations.Clear();
        Assert.True(ListKeys.Press(ctrl, Space));

        Assert.Equal([("PrintSpooler", true)], requests.Calls);
        Assert.Contains("[x] Print Spooler", VisibleOutput());
        Assert.Contains("Starting…", VisibleOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Space_Stops_A_Running_Service_At_Once_And_Keeps_It_Selected()
    {
        RecordedRequests   requests = new();
        WindowsServiceInfo bravo    = Service("Bravo Service", description: "Bravo description.");
        ServicesControl    ctrl     = CreateFocusedControl(
            requests,
            Service("Alpha Service", description: "Alpha description."),
            bravo,
            Service("Charlie Service", description: "Charlie description."));

        ListKeys.Press(ctrl, ConsoleKey.DownArrow);
        runContextHelper.terminal.Invocations.Clear();

        Assert.True(ListKeys.Press(ctrl, Space));

        string output = VisibleOutput();
        string detail = output[output.LastIndexOf("DETAIL", StringComparison.Ordinal)..];

        Assert.Equal([("BravoService", false)], requests.Calls);
        Assert.Contains("[x] Alpha Service", output);
        Assert.Contains("[ ] Bravo Service", output);
        Assert.Contains("Stopping…", detail);
        Assert.Contains("Bravo description.", detail);
        Assert.True(Control.RedrawEnabled);

        // The worker reports the service stopped; the row shows it, still selected.
        bravo.Status = WindowsServiceStatus.Stopped;
        runContextHelper.terminal.Invocations.Clear();
        requests.Complete(ServiceControlResult.Succeeded);

        output = VisibleOutput();
        detail = output[output.LastIndexOf("DETAIL", StringComparison.Ordinal)..];

        Assert.Contains("[ ] Bravo Service", output);
        Assert.Contains("2 running / 3 total", output);
        Assert.Contains("Stopped", detail);
        Assert.Contains("Bravo description.", detail);
        Assert.True(Control.RedrawEnabled);

        ctrl.Unload();
    }

    [Fact]
    public void Space_Typed_Into_The_Filter_Is_Part_Of_The_Filter_Not_A_Toggle()
    {
        RecordedRequests requests = new();
        ServicesControl  ctrl     = CreateFocusedControl(requests, Service("Alpha Service"), Service("Bravo Service"));

        ListKeys.Press(ctrl, ConsoleKey.F);

        foreach (char ch in "bravo service") {
            ListKeys.Press(ctrl, ch == ' ' ? Space : new ConsoleKeyInfo(ch, 0, false, false, false));
        }

        ListKeys.Press(ctrl, ConsoleKey.Enter);

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        Assert.Empty(requests.All);
        Assert.Contains("Filter: bravo service (1 of 2)", VisibleOutput());

        ctrl.Unload();
    }

    public static TheoryData<ServiceControlResult, bool, bool, string> ToggleFailureData() => new() {
        { ServiceControlResult.AccessDenied,         false, false, "It needs administrator rights.\nRun taskmon as administrator." },
        { ServiceControlResult.AccessDenied,         false, true,  "Windows refused the change." },
        { ServiceControlResult.Busy,                 false, false, "It's busy. Try again in a moment." },
        { ServiceControlResult.CannotStop,           false, false, "It doesn't accept a stop request." },
        { ServiceControlResult.DependencyFailed,     true,  false, "A service it depends on couldn't start." },
        { ServiceControlResult.DependentsRunning,    false, false, "Other running services depend on it." },
        { ServiceControlResult.DidNotStop,           false, false, "It's still running." },
        { ServiceControlResult.Disabled,             true,  false, "Its startup type is Disabled." },
        { ServiceControlResult.NotFound,             false, false, "It no longer exists." },
        { ServiceControlResult.NotSupported,         false, false, "Services can't be started or stopped here." },
        { ServiceControlResult.StillPending,         false, false, "It's still stopping after a minute." },
        { ServiceControlResult.StillPending,         true,  false, "It's still starting after a minute." },
        { ServiceControlResult.StoppedAfterStarting, true,  false, "It started and then stopped." },
        { ServiceControlResult.TimedOut,             true,  false, "It didn't respond in time." }
    };

    [Fact]
    public void Toggle_Failure_Text_Cuts_Long_Lines_To_The_Dialog_Width()
    {
        ServiceControlOutcome outcome = new(
            ServiceControlResult.DependentsRunning, 1051, [new string('D', 80), "Fax"]);

        string[] lines = ServicesControl.BuildToggleFailureText(
            new string('S', 80), start: false, outcome, isElevated: false, width: 30).Split('\n');

        Assert.All(lines, line => Assert.True(line.Length <= 30, line));
        Assert.EndsWith("…", lines[0]);
        Assert.EndsWith("…", lines[2]);
    }

    [Fact]
    public void Toggle_Failure_Text_Gives_The_Windows_Error_It_Has_No_Words_For()
    {
        string text = ServicesControl.BuildToggleFailureText(
            "Print Spooler", start: true, new ServiceControlOutcome(ServiceControlResult.Failed, 1072), isElevated: false, width: 56);

        Assert.EndsWith("Windows error 1072.", text);
    }

    [Theory]
    [MemberData(nameof(ToggleFailureData))]
    public void Toggle_Failure_Text_Names_The_Service_And_The_Reason(
        ServiceControlResult result, bool start, bool isElevated, string reason)
    {
        string text = ServicesControl.BuildToggleFailureText(
            "Print Spooler", start, new ServiceControlOutcome(result), isElevated, width: 56);

        Assert.Equal($"Couldn't {(start ? "start" : "stop")} Print Spooler\n{reason}", text);
    }

    [Fact]
    public void Toggle_Failure_Text_Names_Two_Running_Dependents_And_Counts_The_Rest()
    {
        ServiceControlOutcome outcome = new(
            ServiceControlResult.DependentsRunning, 1051, ["Remote Desktop Services", "Fax", "Print Workflow"]);

        string text = ServicesControl.BuildToggleFailureText("Print Spooler", start: false, outcome, isElevated: false, width: 56);

        Assert.Equal(
            "Couldn't stop Print Spooler\nRunning services depend on it:\nRemote Desktop Services, Fax and 1 more",
            text);
    }

    [Fact]
    public void Updates_The_Detail_Pane_When_The_Selection_Moves()
    {
        ServicesControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Service("Windows Update"),
            Service("Fax", description: "Enables you to send and receive faxes.")));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);

        Assert.Contains("Enables you to send and receive faxes.", CapturedOutput());

        ctrl.Unload();
    }

    private string VisibleOutput() => AnsiTestHelper.StripColours(CapturedOutput());

    private string VisibleOutputAfterDraw(Control ctrl)
    {
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();
        return VisibleOutput();
    }

    [Fact]
    public void Without_A_Services_Worker_Space_Says_Services_Cannot_Be_Changed_Here()
    {
        ServicesControl ctrl = CreateControl(canControl: true);
        ctrl.ServicesView.Focused = true;
        ctrl.Sample(SnapshotWith(Service("Print Spooler")));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        Assert.True(ListKeys.Press(ctrl, Space));

        Assert.Contains("Services can't be started or stopped here.", CapturedOutput());

        ListKeys.Press(ctrl, ConsoleKey.Enter);
        ctrl.Unload();
    }
}
