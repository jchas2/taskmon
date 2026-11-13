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

    [Fact]
    public void An_Event_Older_Than_A_Week_Shows_Its_Date()
    {
        DateTime noonToday = DateTime.Today.AddHours(12).ToUniversalTime();

        Assert.Equal(
            noonToday.AddDays(-7).ToLocalTime().ToString("dd MMM HH:mm"),
            HealthControl.DescribeTime(noonToday.AddDays(-7), noonToday));
    }

    [Fact]
    public void An_Interrupted_Event_Does_Not_Say_It_Is_Ongoing()
    {
        HealthControl ctrl = CreateControl();

        HealthInfo info = new() {
            Level     = HealthLevel.Ok,
            Resources = [new HealthReading { Resource = HealthResource.Cpu, Level = HealthLevel.Ok }],
            Events    = [new HealthEvent {
                Resource      = HealthResource.Cpu,
                Level         = HealthLevel.Critical,
                StartUtc      = Now,
                IsInterrupted = true,
                PeakCause     = new HealthCause(HealthCauseKind.CpuBusy, 100.0)
            }]
        };

        ctrl.Sample(Snapshot(info));

        string text = VisibleText();

        Assert.Contains("interrupted, peak 100% busy", text);
        Assert.DoesNotContain("ongoing", text);
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
    public void A_Volume_Under_Pressure_Keeps_Showing_Its_Trend()
    {
        HealthControl ctrl = CreateControl();

        ctrl.Sample(Snapshot(new HealthInfo {
            Level     = HealthLevel.Warning,
            Resources = [new HealthReading {
                Resource = HealthResource.Volumes,
                Level    = HealthLevel.Warning,
                Cause    = new HealthCause(HealthCauseKind.VolumeFull, 95.0, "E:\\"),
                Measures = new VolumesHealthMeasures("E:\\", 95.0, 5L * 1024 * 1024 * 1024, 0.0)
            }]
        }));

        Assert.Contains("E:\\ 95% full", VisibleText());
        Assert.Contains("Not changing", VisibleText());
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
                new HealthReading { Resource = HealthResource.Cpu, Level = HealthLevel.Ok, Measures = new CpuHealthMeasures(12.0, null) },
                new HealthReading { Resource = HealthResource.Gpu }
            ]
        };

    private static HealthInfo NetworkHealth(NetworkHealthMeasures measures) =>
        new() {
            Level     = HealthLevel.Ok,
            Resources = [new HealthReading { Resource = HealthResource.Network, Level = HealthLevel.Ok, Measures = measures }]
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
                    Resource = HealthResource.Memory,
                    Level    = HealthLevel.Warning,
                    Cause    = new HealthCause(HealthCauseKind.CommitCharge, 93.0),
                    Measures = new MemoryHealthMeasures(91.0, 93.0, null, null)
                },
                new HealthReading {
                    Resource = HealthResource.Thermal,
                    Level    = HealthLevel.Critical,
                    Cause    = new HealthCause(HealthCauseKind.CpuPerformanceLimit, 61.0)
                }
            ],
            Culprits = [
                new HealthCulprit {
                    ProcessName  = "chrome",
                    ProcessCount = 14,
                    Pid          = 4242,
                    Resources    = [HealthResource.Memory],
                    MemoryBytes  = 6L * 1024 * 1024 * 1024
                }
            ],
            Events = [
                new HealthEvent {
                    Resource  = HealthResource.Memory,
                    Level     = HealthLevel.Warning,
                    StartUtc  = Now,
                    PeakCause = new HealthCause(HealthCauseKind.CommitCharge, 96.0),
                    Culprits  = ["chrome"]
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
        Assert.Contains("91% in use", text);
        Assert.Contains("chrome (14)", text);
        Assert.Contains("6.0 GB memory", text);
        Assert.Contains("Memory → Warning", text);
        Assert.Contains("ongoing, peak commit 96%", text);
    }

    [Fact]
    public void Pressure_With_No_Cause_Does_Not_Say_Nothing_Is_Under_Pressure()
    {
        HealthControl ctrl = CreateControl();

        HealthInfo info = new() {
            Level     = HealthLevel.Warning,
            Resources = [new HealthReading {
                Resource = HealthResource.Disk,
                Level    = HealthLevel.Warning,
                Cause    = new HealthCause(HealthCauseKind.DiskLatency, 30.0)
            }]
        };

        ctrl.Sample(Snapshot(info));

        string text = VisibleText();

        Assert.Contains("latency 30 ms", text);
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
                new HealthReport {
                    TimeUtc = nowUtc.AddMinutes(-1),
                    Cause   = HealthReportCause.AppError,
                    App     = "chrome.exe",
                    Module  = "ntdll.dll"
                },
                new HealthReport { TimeUtc = nowUtc.AddMinutes(-9), Cause = HealthReportCause.BadBlock, Driver = "disk" }
            ]
        }));

        Assert.Equal(3, ctrl.EventsView.Items.Count);
        Assert.Equal("App crash", ctrl.EventsView.Items[0].SubItems[1].Text);
        Assert.Equal("chrome.exe, in ntdll.dll", ctrl.EventsView.Items[0].SubItems[2].Text);
        Assert.Equal("chrome.exe", ctrl.EventsView.Items[0].SubItems[3].Text);
        Assert.Equal("CPU → Warning", ctrl.EventsView.Items[1].SubItems[1].Text);
        Assert.Equal("Disk error", ctrl.EventsView.Items[2].SubItems[1].Text);
        Assert.Equal("Bad block", ctrl.EventsView.Items[2].SubItems[2].Text);
    }

    private static SystemSnapshot Snapshot(HealthInfo info) => new() { Health = info };

    // Regression test: the network rate was always shown in MB/s, so everyday traffic read
    // "↓ 0.0 ↑ 0.0 MB/s" while the Summary charts showed it moving in KB/s.
    [Fact]
    public void The_Network_Row_Shows_Light_Traffic_In_KB_Per_Second()
    {
        HealthControl ctrl = CreateControl();

        ctrl.Sample(Snapshot(NetworkHealth(new NetworkHealthMeasures(20_480.0, 3_072.0, 0.0, 0.0))));

        Assert.Contains("↓ 20.0 KB/s ↑ 3.0 KB/s", VisibleText());
    }

    [Fact]
    public void The_Selected_Cause_Survives_A_Refresh()
    {
        HealthControl ctrl = CreateControl();

        HealthInfo twoCauses = new() {
            Level     = HealthLevel.Warning,
            Resources = [new HealthReading { Resource = HealthResource.Cpu, Level = HealthLevel.Warning }],
            Culprits  = [
                new HealthCulprit { ProcessName = "alpha", Pid = 1, Resources = [HealthResource.Cpu], CpuPercent = 10.0 },
                new HealthCulprit { ProcessName = "bravo", Pid = 2, Resources = [HealthResource.Cpu], CpuPercent = 20.0 }
            ]
        };

        ctrl.Sample(Snapshot(twoCauses));

        bool handled = false;
        ctrl.CausesView.KeyPressed(ControlHelper.GetConsoleKeyInfo(ConsoleKey.DownArrow), ref handled);

        HealthInfo changed = new() {
            Level     = twoCauses.Level,
            Resources = twoCauses.Resources,
            Culprits  = [twoCauses.Culprits[0], twoCauses.Culprits[1] with { CpuPercent = 25.0 }]
        };

        ctrl.Sample(Snapshot(changed));

        Assert.Equal(1, ctrl.CausesView.SelectedIndex);
    }

    [Fact]
    public void The_Utilisation_Column_Fits_The_Widest_Network_Rates()
    {
        HealthControl ctrl = CreateControl(width: 120);

        ctrl.Sample(Snapshot(NetworkHealth(new NetworkHealthMeasures(1_048_473.0, 1_048_473.0, 0.0, 0.0))));

        Assert.Contains("↓ 1023.9 KB/s ↑ 1023.9 KB/s", VisibleText());
    }

    private string VisibleText() =>
        Regex.Replace(CapturedOutput(), @"\x1b\[[0-9;]*[A-Za-z]", string.Empty);
}
