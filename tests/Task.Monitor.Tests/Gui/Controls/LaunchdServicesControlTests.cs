using Task.Monitor.Gui.Controls.LaunchdServices;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.LaunchdServices;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class LaunchdServicesControlTests : IDisposable
{
    private readonly RunContextHelper runContextHelper;
    private readonly RunContext       runContext;

    public LaunchdServicesControlTests()
    {
        runContextHelper = new RunContextHelper();
        runContext       = runContextHelper.GetRunContext();
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
        LaunchdServicesControl ctrl = new(runContext.ServiceController, runContext.Terminal, runContext.AppConfig);

        Assert.NotNull(ctrl);
    }

    private LaunchdServicesControl CreateControl(int width = 180, int height = 50)
    {
        LaunchdServicesControl ctrl = new(
            runContext.ServiceController,
            new ForwardingTerminal(runContext.Terminal),
            runContext.AppConfig) {
            Width  = width,
            Height = height
        };

        ctrl.Load();
        ctrl.Resize();
        return ctrl;
    }

    [Fact]
    public void Describes_A_Signal_And_An_Exit_Code_In_The_Last_Exit_Row()
    {
        LaunchdServicesControl ctrl = CreateControl();

        // 9 is a SIGKILL; 256 is exit code 1.
        ctrl.Sample(SnapshotWith(Service("com.example.killed", lastExitStatus: 9)));
        ctrl.Draw();
        Assert.Contains("Signal 9", CapturedOutput());

        ctrl.Sample(SnapshotWith(Service("com.example.failed", lastExitStatus: 256)));
        ctrl.Draw();
        Assert.Contains("Exit 1", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Detail_Pane_Follows_The_Selection_In_The_Filtered_List()
    {
        LaunchdServicesControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Service("com.alpha.agent", programPath: "/usr/libexec/alpha"),
            Service("com.bravo.agent", status: LaunchdServiceStatus.Waiting, programPath: "/usr/libexec/bravo"),
            Service("com.charlie.agent", programPath: "/usr/libexec/charlie")));
        ctrl.Draw();

        FilterKeys.Apply(ctrl, "charlie");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("/usr/libexec/charlie", output);
        Assert.DoesNotContain("/usr/libexec/alpha", output);

        ctrl.Unload();
    }

    public void Dispose() => Control.RedrawEnabled = true;

    [Fact]
    public void Draws_A_Detail_Pane_For_The_Selected_Service()
    {
        LaunchdServicesControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(Service(
            "com.apple.syslogd",
            programPath: "/usr/sbin/syslogd",
            arguments:   "-debug",
            publisher:   "Apple Inc.",
            plistPath:   "/System/Library/LaunchDaemons/com.apple.syslogd.plist")));
        ctrl.Draw();

        string output = CapturedOutput();

        foreach (string fragment in new[] {
            "FIELD", "VALUE", "Label", "Domain", "Group", "Status", "Start Type", "Enabled", "Last Exit",
            "Program", "Arguments", "Publisher", "Plist",
            "com.apple.syslogd", "System", "-debug", "Apple Inc.", "/System/Library/LaunchDaemons/com.apple.syslogd.plist"
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Column_Headers_And_A_Row_Per_Service()
    {
        LaunchdServicesControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Service("com.apple.syslogd",   status: LaunchdServiceStatus.Running,   pid: 321, startType: LaunchdStartType.KeepAlive),
            Service("com.apple.netbiosd",  status: LaunchdServiceStatus.Waiting,   startType: LaunchdStartType.OnDemand),
            Service("com.example.updater", status: LaunchdServiceStatus.NotLoaded, startType: LaunchdStartType.AtLoad, disabled: true),
            Service("com.example.unknown", status: LaunchdServiceStatus.Unknown)));
        ctrl.Draw();

        string output = CapturedOutput();

        foreach (string fragment in new[] {
            "LABEL", "GROUP", "STATUS", "START", "ENABLED", "PATH",
            "Running (321)", "Keep Alive", "Waiting", "On Demand", "Not Loaded", "At Load", "Unknown", "No", "Yes"
        }) {
            Assert.Contains(fragment, output);
        }

        ctrl.Unload();
    }

    [Fact]
    public void Draws_The_Empty_Text_Before_A_Snapshot_Arrives()
    {
        LaunchdServicesControl ctrl = CreateControl();
        ctrl.Draw();

        Assert.Contains("Gathering launchd services", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Escape_Clears_The_Filter()
    {
        LaunchdServicesControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Service("com.alpha.agent", programPath: "/usr/libexec/alpha"),
            Service("com.bravo.agent", status: LaunchdServiceStatus.Waiting, programPath: "/usr/libexec/bravo"),
            Service("com.charlie.agent", programPath: "/usr/libexec/charlie")));
        ctrl.Draw();
        FilterKeys.Apply(ctrl, "bravo");

        Assert.True(FilterKeys.Press(ctrl, ConsoleKey.Escape));

        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("com.alpha.agent", output);
        Assert.Contains("com.charlie.agent", output);
        Assert.DoesNotContain("Filter:", output);

        ctrl.Unload();
    }

    [Fact]
    public void Filter_Matches_A_Process_Id()
    {
        LaunchdServicesControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Service("com.alpha.agent", pid: 4321),
            Service("com.bravo.agent", pid: 999)));
        ctrl.Draw();

        FilterKeys.Apply(ctrl, "4321");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        Assert.Contains("com.alpha.agent", CapturedOutput());
        Assert.DoesNotContain("com.bravo.agent", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void Filter_Shows_Only_Matching_Rows_And_The_Match_Count_In_The_Title()
    {
        LaunchdServicesControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Service("com.alpha.agent", programPath: "/usr/libexec/alpha"),
            Service("com.bravo.agent", status: LaunchdServiceStatus.Waiting, programPath: "/usr/libexec/bravo"),
            Service("com.charlie.agent", programPath: "/usr/libexec/charlie")));
        ctrl.Draw();

        FilterKeys.Apply(ctrl, "bravo");
        runContextHelper.terminal.Invocations.Clear();
        ctrl.Draw();

        string output = CapturedOutput();
        Assert.Contains("Filter: bravo (1 of 3)", output);
        Assert.Contains("com.bravo.agent", output);
        Assert.DoesNotContain("com.alpha.agent", output);
        Assert.DoesNotContain("com.charlie.agent", output);

        ctrl.Unload();
    }

    [Fact]
    public void Footer_Shows_The_Clear_Hint_Only_While_Filtered_And_Counts_All_Entries()
    {
        LaunchdServicesControl ctrl = CreateControl();
        ctrl.Sample(SnapshotWith(
            Service("com.alpha.agent", programPath: "/usr/libexec/alpha"),
            Service("com.bravo.agent", status: LaunchdServiceStatus.Waiting, programPath: "/usr/libexec/bravo"),
            Service("com.charlie.agent", programPath: "/usr/libexec/charlie")));
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

    [Fact]
    public void HasFocus_Survives_The_OnGotFocus_Redirect()
    {
        ForwardingTerminal terminal = new(runContext.Terminal);
        Screen screen = new(terminal) { Width = 180, Height = 50 };

        LaunchdServicesControl ctrl = new(runContext.ServiceController, terminal, runContext.AppConfig) {
            Width  = 180,
            Height = 50
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
    public void Ignores_A_Snapshot_With_No_Launchd_Services_Info()
    {
        LaunchdServicesControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());
        ctrl.Draw();

        Assert.Contains("Gathering launchd services", CapturedOutput());

        ctrl.Unload();
    }

    [Fact]
    public void R_Without_A_Registered_LaunchdServicesService_Does_Not_Throw()
    {
        LaunchdServicesControl ctrl = CreateControl();
        ctrl.Draw();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('r', ConsoleKey.R, false, false, false), ref handled);

        Assert.True(handled);

        ctrl.Unload();
    }

    private static LaunchdServiceInfo Service(
        string               label,
        LaunchdServiceStatus status         = LaunchdServiceStatus.Running,
        int?                 pid            = 100,
        LaunchdStartType     startType      = LaunchdStartType.OnDemand,
        bool                 disabled       = false,
        int?                 lastExitStatus = 0,
        string?              programPath    = "/usr/libexec/example",
        string?              arguments      = null,
        string?              publisher      = null,
        string?              plistPath      = null) =>
        new() {
            Label          = label,
            Domain         = LaunchdDomain.System,
            Group          = "/System/Library/LaunchDaemons",
            Status         = status,
            Pid            = status == LaunchdServiceStatus.Running ? pid : null,
            StartType      = startType,
            Disabled       = disabled,
            LastExitStatus = lastExitStatus,
            ProgramPath    = programPath,
            Arguments      = arguments,
            Publisher      = publisher,
            PlistPath      = plistPath
        };

    // Waiting is launchd's normal on-demand state, so only Running counts towards the total.
    [Fact]
    public void Shows_The_Running_And_Total_Counts_In_The_Footer()
    {
        LaunchdServicesControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Service("a", status: LaunchdServiceStatus.Running),
            Service("b", status: LaunchdServiceStatus.Waiting),
            Service("c", status: LaunchdServiceStatus.NotLoaded)));
        ctrl.Draw();

        Assert.Contains("1 running / 3 total", CapturedOutput());

        ctrl.Unload();
    }

    private static SystemSnapshot SnapshotWith(params LaunchdServiceInfo[] services) =>
        new() { LaunchdServices = new LaunchdServicesInfo { Specs = new LaunchdServicesSpecs { Services = [.. services] } } };

    [Fact]
    public void Updates_The_Detail_Pane_When_The_Selection_Moves()
    {
        LaunchdServicesControl ctrl = CreateControl();

        ctrl.Sample(SnapshotWith(
            Service("com.example.first"),
            Service("com.example.second", plistPath: "/Library/LaunchDaemons/com.example.second.plist")));
        ctrl.Draw();
        runContextHelper.terminal.Invocations.Clear();

        bool handled = false;
        ctrl.KeyPressed(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), ref handled);

        Assert.Contains("/Library/LaunchDaemons/com.example.second.plist", CapturedOutput());

        ctrl.Unload();
    }
}
