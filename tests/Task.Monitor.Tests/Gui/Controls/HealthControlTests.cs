using System.Text.RegularExpressions;
using Task.Monitor.Gui.Controls.Health;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Health;
using Task.Monitor.System.Tests.Controls;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class HealthControlTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly RunContextHelper runContextHelper;
    private readonly RunContext       runContext;

    public HealthControlTests()
    {
        runContextHelper = new RunContextHelper();
        runContext       = runContextHelper.GetRunContext();
    }

    // The view is 64 wide on an 80 column terminal, and the reason for a warning has to survive that.
    [Fact]
    public void A_Narrow_Terminal_Keeps_The_Reason_For_A_Warning()
    {
        HealthControl ctrl = CreateControl(width: 64);

        ctrl.Sample(Snapshot(PressuredInfo()));

        Assert.Contains("commit 93%", VisibleText());
        Assert.Contains("CPU limited to 61%", VisibleText());
        Assert.DoesNotContain("ERRORS", VisibleText());
    }

    [Fact]
    public void A_Short_Terminal_Hides_The_Panes_It_Cannot_Fit()
    {
        HealthControl ctrl = CreateControl(height: 12);

        ctrl.Sample(Snapshot(HealthyInfo()));

        Assert.Contains("RESOURCE", VisibleText());
        Assert.DoesNotContain("LIKELY CAUSES", VisibleText());
        Assert.DoesNotContain("RECENT EVENTS", VisibleText());
    }

    [Fact]
    public void A_Wide_Terminal_Shows_The_Errors_Column()
    {
        HealthControl ctrl = CreateControl(width: 104);

        ctrl.Sample(Snapshot(PressuredInfo()));

        Assert.Contains("ERRORS", VisibleText());
    }

    private string CapturedOutput() =>
        string.Concat(runContextHelper.terminal.Invocations
            .Where(invocation => invocation.Method.Name == "Write"
                && invocation.Arguments.Count == 1
                && invocation.Arguments[0] is string)
            .Select(invocation => (string)invocation.Arguments[0]!));

    private HealthControl CreateControl(int width = 140, int height = 40)
    {
        HealthControl ctrl = new(
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
    public void Earlier_Days_Show_The_Day_As_Well_As_The_Time()
    {
        DateTime noonToday = DateTime.Today.AddHours(12).ToUniversalTime();

        Assert.Equal(noonToday.ToLocalTime().ToString("HH:mm:ss"), HealthControl.DescribeTime(noonToday, noonToday));
        Assert.Equal(
            noonToday.AddDays(-1).ToLocalTime().ToString("ddd HH:mm"),
            HealthControl.DescribeTime(noonToday.AddDays(-1), noonToday));
    }

    [Fact]
    public void Enter_On_A_Cause_Requests_That_Process()
    {
        HealthControl ctrl = CreateControl();
        ctrl.Sample(Snapshot(PressuredInfo()));

        int? requested = null;
        ctrl.ProcessRequested += (_, pid) => requested = pid;

        bool handled = false;
        ctrl.CausesView.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.Enter), ref handled);

        Assert.Equal(4242, requested);
    }

    [Fact]
    public void Headline_Names_The_Resources_Under_Pressure_And_When_It_Began()
    {
        string headline = HealthControl.DescribeHeadline(PressuredInfo());

        Assert.StartsWith("Under pressure: Memory, ", headline);
        Assert.Contains($"since {Now.ToLocalTime():HH:mm:ss}", headline);
    }

    [Fact]
    public void Headline_Says_Healthy_When_All_Is_Well()
    {
        Assert.Equal("Healthy", HealthControl.DescribeHeadline(HealthyInfo()));
    }

    private static HealthInfo HealthyInfo() =>
        new() {
            Level     = HealthLevel.Ok,
            Resources = [
                new HealthReading { Resource = HealthResource.Cpu, Level = HealthLevel.Ok, Utilisation = "12%" },
                new HealthReading { Resource = HealthResource.Gpu }
            ]
        };

    [Fact]
    public void No_Health_Data_Yet_Draws_Without_Failing()
    {
        HealthControl ctrl = CreateControl();

        ctrl.Sample(new SystemSnapshot());
        ctrl.Draw();

        Assert.Contains("Gathering metrics", VisibleText());
    }

    private static HealthInfo PressuredInfo() =>
        new() {
            Level     = HealthLevel.Warning,
            SinceUtc  = Now,
            Resources = [
                new HealthReading {
                    Resource    = HealthResource.Memory,
                    Level       = HealthLevel.Warning,
                    Reason      = "commit 93%",
                    Utilisation = "91% in use"
                },
                new HealthReading { Resource = HealthResource.Thermal, Level = HealthLevel.Critical, Reason = "CPU limited to 61%" }
            ],
            Culprits = [
                new HealthCulprit("chrome", 14, 4242, "6 GB memory", [HealthResource.Memory])
            ],
            Events = [
                new HealthEvent {
                    Resource   = HealthResource.Memory,
                    Level      = HealthLevel.Warning,
                    StartUtc   = Now,
                    PeakReason = "commit 96%",
                    Culprits   = ["chrome"]
                }
            ]
        };

    [Fact]
    public void Pressure_Draws_The_Reason_Causes_And_Events()
    {
        HealthControl ctrl = CreateControl();

        ctrl.Sample(Snapshot(PressuredInfo()));

        string text = VisibleText();

        Assert.Contains("Warning", text);
        Assert.Contains("commit 93%", text);
        Assert.Contains("chrome (14)", text);
        Assert.Contains("6 GB memory", text);
        Assert.Contains("Memory → Warning", text);
        Assert.Contains("ongoing, peak commit 96%", text);
    }

    [Fact]
    public void Pressure_With_No_Cause_Does_Not_Say_Nothing_Is_Under_Pressure()
    {
        HealthControl ctrl = CreateControl();

        HealthInfo info = new() {
            Level     = HealthLevel.Warning,
            Resources = [new HealthReading { Resource = HealthResource.Disk, Level = HealthLevel.Warning, Reason = "latency 30 ms" }]
        };

        ctrl.Sample(Snapshot(info));

        string text = VisibleText();

        Assert.Contains("No process stands out.", text);
        Assert.DoesNotContain("Nothing under pressure.", text);
    }

    [Fact]
    public void Reports_Are_Listed_With_The_Events_Newest_First()
    {
        HealthControl ctrl = CreateControl();

        DateTime nowUtc = DateTime.UtcNow;

        ctrl.Sample(Snapshot(new HealthInfo {
            Level     = HealthLevel.Ok,
            Resources = [new HealthReading { Resource = HealthResource.Cpu, Level = HealthLevel.Ok }],
            Events    = [new HealthEvent {
                Resource = HealthResource.Cpu,
                Level    = HealthLevel.Warning,
                StartUtc = nowUtc.AddMinutes(-5),
                EndUtc   = nowUtc.AddMinutes(-2)
            }],
            Reports = [
                new HealthReport(nowUtc.AddMinutes(-1), HealthReportKind.AppCrash, "App crash", "chrome.exe, in ntdll.dll", "chrome.exe"),
                new HealthReport(nowUtc.AddMinutes(-9), HealthReportKind.DiskError, "Disk error", "Bad block", string.Empty)
            ]
        }));

        Assert.Equal(3, ctrl.EventsView.Items.Count);
        Assert.Equal("App crash", ctrl.EventsView.Items[0].SubItems[1].Text);
        Assert.Equal("CPU → Warning", ctrl.EventsView.Items[1].SubItems[1].Text);
        Assert.Equal("Disk error", ctrl.EventsView.Items[2].SubItems[1].Text);
    }

    private static SystemSnapshot Snapshot(HealthInfo info) => new() { Health = info };

    [Fact]
    public void The_Selected_Cause_Survives_A_Refresh()
    {
        HealthControl ctrl = CreateControl();

        HealthInfo twoCauses = new() {
            Level     = HealthLevel.Warning,
            Resources = [new HealthReading { Resource = HealthResource.Cpu, Level = HealthLevel.Warning }],
            Culprits  = [
                new HealthCulprit("alpha", 1, 1, "10% CPU", [HealthResource.Cpu]),
                new HealthCulprit("bravo", 1, 2, "20% CPU", [HealthResource.Cpu])
            ]
        };

        ctrl.Sample(Snapshot(twoCauses));

        bool handled = false;
        ctrl.CausesView.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);

        HealthInfo changed = new() {
            Level     = twoCauses.Level,
            Resources = twoCauses.Resources,
            Culprits  = [twoCauses.Culprits[0], twoCauses.Culprits[1] with { Detail = "25% CPU" }]
        };

        ctrl.Sample(Snapshot(changed));

        Assert.Equal(1, ctrl.CausesView.SelectedIndex);
    }

    private string VisibleText() =>
        Regex.Replace(CapturedOutput(), @"\x1b\[[0-9;]*[A-Za-z]", string.Empty);
}
