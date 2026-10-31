using Task.Monitor.Gui.Controls.Services;
using Task.Monitor.System.Controls;
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

    public ServicesControlTests(ITestOutputHelper outputHelper)
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
        ServicesControl ctrl = new(
            runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private ServicesControl CreateControl(int width = 160, int height = 40)
    {
        ServicesControl ctrl = new(
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
    public void Descending_Sort_Reverses_The_Rows_And_The_Detail_Pane_Follows_The_Selection()
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

        Assert.Contains("NAME\u2193", output);
        Assert.True(output.IndexOf("Bravo Service", StringComparison.Ordinal) < output.IndexOf("Alpha Service", StringComparison.Ordinal));
        Assert.Contains("Bravo Service", detail);
        Assert.DoesNotContain("Alpha Service", detail);

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

    public void Dispose() => Control.RedrawEnabled = true;

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

    private static WindowsServiceInfo Service(
        string displayName,
        string serviceName = "Svc",
        WindowsServiceStatus status = WindowsServiceStatus.Running,
        WindowsServiceStartType startType = WindowsServiceStartType.AutomaticStart,
        bool delayedAutoStart = false,
        string? logOnAs = "LocalSystem",
        string? description = "A service.") =>
        new() {
            ServiceName = serviceName,
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
}
