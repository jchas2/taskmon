using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Health;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class HealthEvaluatorTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly HealthEvaluator  evaluator  = new();
    private readonly HealthThresholds thresholds = new();

    [Fact]
    public void An_Event_Starts_When_The_Condition_Began_Not_When_It_Was_Raised()
    {
        Run(Busy(0.20), from: 0, to: 10);

        HealthInfo info = Run(Busy(0.95), from: 15, to: 45);

        Assert.Equal(Start.AddSeconds(15), Assert.Single(info.Events).StartUtc);
        Assert.Equal(Start.AddSeconds(15), info.SinceUtc);
    }

    [Fact]
    public void A_Brief_Dip_Does_Not_Clear_The_Status()
    {
        Run(Busy(0.95), from: 0, to: 40);
        Run(Busy(0.20), from: 40, to: 50);

        HealthInfo info = Run(Busy(0.95), from: 50, to: 55);

        Assert.Equal(HealthLevel.Warning, CpuLevel(info));
    }

    [Fact]
    public void A_Change_To_The_Update_Interval_Does_Not_Change_The_Timing()
    {
        HealthEvaluator slower = new();

        Assert.Equal(HealthLevel.Warning, CpuLevel(Run(Busy(0.95), from: 0, to: 30, step: 1)));

        for (int seconds = 0; seconds <= 30; seconds += 10) {
            slower.Evaluate(Busy(0.95), thresholds, Start.AddSeconds(seconds));
        }

        Assert.Equal(HealthLevel.Warning, CpuLevel(slower.Evaluate(Busy(0.95), thresholds, Start.AddSeconds(30))));
    }

    [Fact]
    public void A_Cleared_Status_Closes_Its_Event()
    {
        Run(Busy(0.95), from: 0, to: 40);

        HealthInfo info = Run(Busy(0.20), from: 40, to: 75);

        HealthEvent healthEvent = Assert.Single(info.Events);
        Assert.Equal(HealthLevel.Ok, CpuLevel(info));
        Assert.Equal(HealthResource.Cpu, healthEvent.Resource);
        Assert.NotNull(healthEvent.EndUtc);
    }

    [Fact]
    public void A_Dip_Longer_Than_The_Grace_Period_Restarts_The_Sustain_Timer()
    {
        Run(Busy(0.95), from: 0, to: 15);
        Run(Busy(0.50), from: 20, to: 30);

        Assert.Equal(HealthLevel.Ok, CpuLevel(Run(Busy(0.95), from: 35, to: 60)));
        Assert.Equal(HealthLevel.Warning, CpuLevel(Run(Busy(0.95), from: 65, to: 65)));
    }

    [Fact]
    public void A_Falling_Value_Clears_After_The_Clear_Period()
    {
        Run(Busy(0.95), from: 0, to: 40);

        Assert.Equal(HealthLevel.Warning, CpuLevel(Run(Busy(0.20), from: 40, to: 65)));
        Assert.Equal(HealthLevel.Ok, CpuLevel(Run(Busy(0.20), from: 65, to: 75)));
    }

    [Fact]
    public void A_Hung_App_Is_Named_As_A_Cause_And_Recorded_As_An_Event()
    {
        HealthSample sample = new() { CanDetectHungApps = true, HungApps = [new HungApp(42, "notepad", 12.0)] };

        HealthInfo info = evaluator.Evaluate(sample, thresholds, Start);

        HealthCulprit culprit = Assert.Single(info.Culprits);

        Assert.Equal(42, culprit.Pid);
        Assert.Equal("Not responding for 12 s", culprit.Detail);
        Assert.Equal(HealthLevel.Warning, Level(info, HealthResource.Apps));
        Assert.Equal(["notepad"], Assert.Single(info.Events).Culprits);
    }

    [Fact]
    public void A_New_Peak_Replaces_The_Events_Peak_And_Culprits()
    {
        Run(Busy(0.92), from: 0, to: 40);

        HealthInfo info = Run(Busy(0.96), from: 40, to: 45);

        Assert.Contains("96%", Assert.Single(info.Events).PeakReason);
    }

    // Regression test: on a real run the CPU averaged 99.7% and never reached Critical, as one
    // sample at 97.7% was enough to restart the timer.
    [Fact]
    public void A_One_Sample_Dip_Does_Not_Restart_The_Critical_Timer()
    {
        Run(Busy(0.99), from: 0, to: 40);
        evaluator.Evaluate(Busy(0.97), thresholds, Start.AddSeconds(42));

        Assert.Equal(HealthLevel.Critical, CpuLevel(Run(Busy(0.99), from: 45, to: 60)));
    }

    [Fact]
    public void A_One_Sample_Dip_Does_Not_Restart_The_Warning_Timer()
    {
        Run(Busy(0.95), from: 0, to: 20);
        evaluator.Evaluate(Busy(0.89), thresholds, Start.AddSeconds(22));

        Assert.Equal(HealthLevel.Warning, CpuLevel(Run(Busy(0.95), from: 25, to: 30)));
    }

    [Fact]
    public void A_Peak_Is_Kept_After_The_Value_Falls()
    {
        Run(Busy(0.97), from: 0, to: 40);

        HealthInfo info = Run(Busy(0.91), from: 40, to: 45);

        Assert.Contains("97%", Assert.Single(info.Events).PeakReason);
    }

    [Fact]
    public void A_Resource_With_Nothing_To_Report_Does_Not_Affect_The_Others()
    {
        HealthInfo info = Run(Busy(0.20), from: 0, to: 5);

        Assert.Equal(HealthLevel.Ok, info.Level);
        Assert.Equal(HealthLevel.Unknown, Level(info, HealthResource.Gpu));
    }

    [Fact]
    public void A_Short_Hang_Is_Not_Named()
    {
        HealthSample sample = new() { CanDetectHungApps = true, HungApps = [new HungApp(42, "notepad", 2.0)] };

        Assert.Empty(evaluator.Evaluate(sample, thresholds, Start).Culprits);
    }

    [Fact]
    public void A_Short_Spike_Does_Not_Raise_A_Status()
    {
        Run(Busy(0.99), from: 0, to: 25);

        HealthInfo info = Run(Busy(0.10), from: 25, to: 30);

        Assert.Equal(HealthLevel.Ok, CpuLevel(info));
        Assert.Empty(info.Events);
    }

    // Regression test: one sample of 6,206 hard faults/s restarted the clear timer.
    [Fact]
    public void A_Single_Spike_Does_Not_Restart_The_Clear_Timer()
    {
        Run(Busy(0.95), from: 0, to: 40);
        Run(Busy(0.20), from: 45, to: 60);
        evaluator.Evaluate(Busy(0.95), thresholds, Start.AddSeconds(62));

        Assert.Equal(HealthLevel.Ok, CpuLevel(Run(Busy(0.20), from: 65, to: 75)));
    }

    [Fact]
    public void A_Spike_That_Has_Ended_Cannot_Raise_A_Status_During_Its_Grace_Period()
    {
        Run(Busy(0.95), from: 0, to: 28);

        Assert.Equal(HealthLevel.Ok, CpuLevel(evaluator.Evaluate(Busy(0.10), thresholds, Start.AddSeconds(31))));
        Assert.Equal(HealthLevel.Ok, CpuLevel(evaluator.Evaluate(Busy(0.10), thresholds, Start.AddSeconds(33))));
    }

    [Fact]
    public void A_Status_Cannot_Clear_While_The_Value_Is_Over_The_Threshold()
    {
        Run(Busy(0.95), from: 0, to: 40);
        Run(Busy(0.20), from: 45, to: 70);

        Assert.Equal(HealthLevel.Warning, CpuLevel(Run(Busy(0.95), from: 75, to: 75)));
    }

    [Fact]
    public void A_Status_Raised_At_Once_Still_Takes_The_Clear_Period()
    {
        HealthVolume plenty = new("D:\\", 50, 100, false);
        HealthVolume full   = plenty with { FreeBytes = 8 };

        for (int seconds = 0; seconds <= 60; seconds += 5) {
            evaluator.Evaluate(new HealthSample { Volumes = [plenty] }, thresholds, Start.AddSeconds(seconds));
        }

        evaluator.Evaluate(new HealthSample { Volumes = [full] }, thresholds, Start.AddSeconds(65));

        HealthInfo soon  = evaluator.Evaluate(new HealthSample { Volumes = [plenty] }, thresholds, Start.AddSeconds(67));
        HealthInfo later = evaluator.Evaluate(new HealthSample { Volumes = [plenty] }, thresholds, Start.AddSeconds(100));

        Assert.Equal(HealthLevel.Warning, Level(soon, HealthResource.Volumes));
        Assert.Equal(HealthLevel.Ok, Level(later, HealthResource.Volumes));
    }

    [Fact]
    public void A_Sustained_Value_Raises_A_Warning()
    {
        Assert.Equal(HealthLevel.Ok, CpuLevel(Run(Busy(0.95), from: 0, to: 25)));
        Assert.Equal(HealthLevel.Warning, CpuLevel(Run(Busy(0.95), from: 25, to: 30)));
    }

    [Fact]
    public void A_Value_Back_Over_The_Threshold_Does_Restart_The_Clear_Timer()
    {
        Run(Busy(0.95), from: 0, to: 40);
        Run(Busy(0.20), from: 45, to: 55);
        Run(Busy(0.95), from: 60, to: 70);

        Assert.Equal(HealthLevel.Warning, CpuLevel(Run(Busy(0.20), from: 75, to: 100)));
        Assert.Equal(HealthLevel.Ok, CpuLevel(Run(Busy(0.20), from: 105, to: 105)));
    }

    private static HealthSample Busy(double ratio, ProcessInfo? processes = null) =>
        new() {
            Cpu       = new CpuInfo { Metrics = new CpuMetrics { CpuTotalTime = ratio } },
            Processes = processes
        };

    private static HealthLevel CpuLevel(HealthInfo info) => Level(info, HealthResource.Cpu);

    [Fact]
    public void Critical_Clears_In_One_Clear_Period_When_The_Value_Drops_Straight_To_Ok()
    {
        Run(Busy(0.99), from: 0, to: 65);

        Assert.Equal(HealthLevel.Critical, CpuLevel(Run(Busy(0.20), from: 70, to: 95)));
        Assert.Equal(HealthLevel.Ok, CpuLevel(Run(Busy(0.20), from: 100, to: 100)));
    }

    [Fact]
    public void Critical_Needs_Its_Own_Longer_Hold()
    {
        Assert.Equal(HealthLevel.Warning, CpuLevel(Run(Busy(0.99), from: 0, to: 55)));
        Assert.Equal(HealthLevel.Critical, CpuLevel(Run(Busy(0.99), from: 55, to: 60)));
    }

    [Fact]
    public void Culprits_Are_Only_Named_While_A_Resource_Is_Under_Pressure()
    {
        HealthSample sample = Busy(0.95, Processes(new ProcessEntry {
            Pid = 7, ProcessName = "miner", CpuTimePercent = 0.9
        }));

        Assert.Empty(Run(sample, from: 0, to: 25).Culprits);

        HealthCulprit culprit = Assert.Single(Run(sample, from: 25, to: 30).Culprits);

        Assert.Equal("miner", culprit.ProcessName);
        Assert.Equal(7, culprit.Pid);
        Assert.Equal([HealthResource.Cpu], culprit.Resources);
    }

    [Fact]
    public void Event_History_Keeps_The_Newest_And_Drops_The_Oldest()
    {
        HealthThresholds small = new() { EventHistorySize = 2 };

        for (int episode = 0; episode < 3; episode++) {
            int offset = episode * 200;

            for (int seconds = 0; seconds <= 40; seconds += 5) {
                evaluator.Evaluate(Busy(0.95), small, Start.AddSeconds(offset + seconds));
            }

            for (int seconds = 45; seconds <= 90; seconds += 5) {
                evaluator.Evaluate(Busy(0.10), small, Start.AddSeconds(offset + seconds));
            }
        }

        HealthInfo info = evaluator.Evaluate(Busy(0.10), small, Start.AddSeconds(700));

        Assert.Equal(2, info.Events.Count);
        Assert.Equal(Start.AddSeconds(400), info.Events[0].StartUtc);
        Assert.Equal(Start.AddSeconds(200), info.Events[1].StartUtc);
    }

    // Regression test: the clear timer started when the value left Critical, so a disk that then
    // left Warning went straight to OK 2 seconds after its load ended.
    [Fact]
    public void Leaving_Warning_Gets_Its_Own_Clear_Period_After_Leaving_Critical()
    {
        Run(Busy(0.99), from: 0, to: 65);
        Run(Busy(0.95), from: 70, to: 95);

        Assert.Equal(HealthLevel.Warning, CpuLevel(Run(Busy(0.20), from: 100, to: 100)));
        Assert.Equal(HealthLevel.Warning, CpuLevel(Run(Busy(0.20), from: 105, to: 125)));
        Assert.Equal(HealthLevel.Ok, CpuLevel(Run(Busy(0.20), from: 130, to: 130)));
    }

    private static HealthLevel Level(HealthInfo info, HealthResource resource) =>
        info.Resources.Single(reading => reading.Resource == resource).Level;

    [Fact]
    public void Nothing_Reported_Is_Unknown()
    {
        HealthInfo info = evaluator.Evaluate(new HealthSample(), thresholds, Start);

        Assert.Equal(HealthLevel.Unknown, info.Level);
        Assert.Equal(Enum.GetValues<HealthResource>().Length, info.Resources.Count);
    }

    [Fact]
    public void Overall_Level_Is_The_Worst_Resource()
    {
        HealthSample sample = Busy(0.95) with { Volumes = [new HealthVolume("C:\\", 1, 100, false)] };

        HealthInfo info = evaluator.Evaluate(sample, thresholds, Start);

        Assert.Equal(HealthLevel.Critical, info.Level);
        Assert.Equal(HealthLevel.Ok, CpuLevel(info));
    }

    private static ProcessInfo Processes(params ProcessEntry[] entries)
    {
        ProcessInfo info = new();
        info.Metrics.Entries.AddRange(entries);
        return info;
    }

    // Regression test: with a grace per dip, hard fault bursts a few seconds apart kept the timer
    // running, so a single burst raised a Warning straight after the status had cleared.
    [Fact]
    public void Regular_Spikes_Cannot_Keep_A_Sustain_Timer_Running()
    {
        for (int seconds = 0; seconds <= 120; seconds += 2) {
            HealthSample sample = seconds % 6 == 0 ? Busy(0.95) : Busy(0.20);

            Assert.Equal(HealthLevel.Ok, CpuLevel(evaluator.Evaluate(sample, thresholds, Start.AddSeconds(seconds))));
        }
    }

    [Fact]
    public void Reports_Are_Passed_Through_Without_Changing_The_Overall_Level()
    {
        HealthReport report = new(Start, HealthReportKind.DiskError, "Disk error", "Bad block", string.Empty);

        HealthInfo info = evaluator.Evaluate(Busy(0.10) with { Reports = [report] }, thresholds, Start);

        Assert.Equal(HealthLevel.Ok, info.Level);
        Assert.Equal([report], info.Reports);
    }

    private HealthInfo Run(HealthSample sample, int from, int to, int step = 5)
    {
        HealthInfo? info = null;

        for (int seconds = from; seconds <= to; seconds += step) {
            info = evaluator.Evaluate(sample, thresholds, Start.AddSeconds(seconds));
        }

        return info!;
    }

    [Fact]
    public void Settings_Change_The_Timing()
    {
        HealthThresholds quick = new() { WarningSustainSeconds = 5 };

        evaluator.Evaluate(Busy(0.95), quick, Start);

        HealthInfo info = evaluator.Evaluate(Busy(0.95), quick, Start.AddSeconds(5));

        Assert.Equal(HealthLevel.Warning, CpuLevel(info));
    }

    [Fact]
    public void The_Grace_Period_Is_A_Setting()
    {
        HealthThresholds patient = new() { SustainGraceSeconds = 20 };

        for (int seconds = 0; seconds <= 10; seconds += 5) {
            evaluator.Evaluate(Busy(0.95), patient, Start.AddSeconds(seconds));
        }

        evaluator.Evaluate(Busy(0.50), patient, Start.AddSeconds(12));
        evaluator.Evaluate(Busy(0.50), patient, Start.AddSeconds(25));

        HealthInfo info = evaluator.Evaluate(Busy(0.95), patient, Start.AddSeconds(30));

        Assert.Equal(HealthLevel.Warning, CpuLevel(info));
    }

    [Fact]
    public void Volumes_Skip_The_Sustain_Period()
    {
        HealthSample sample = new() { Volumes = [new HealthVolume("D:\\", 8, 100, false)] };

        HealthInfo info = evaluator.Evaluate(sample, thresholds, Start);

        Assert.Equal(HealthLevel.Warning, Level(info, HealthResource.Volumes));
    }
}
