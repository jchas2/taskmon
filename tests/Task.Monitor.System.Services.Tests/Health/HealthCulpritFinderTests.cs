using Task.Monitor.System.Services.Health;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class HealthCulpritFinderTests
{
    private const long Megabyte = 1024L * 1024L;

    private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly HealthCulpritFinder finder = new();

    // Regression test: 36 yes processes used 996% CPU between them, and a browser at 84% was named
    // alongside them as if it shared the blame.
    [Fact]
    public void A_Process_Far_Behind_The_Top_User_Is_Not_Named()
    {
        finder.Update(Processes(Entry("yes", 1, cpu: 9.96), Entry("browser", 2, cpu: 0.84)), Start);

        Assert.Equal(["yes"], Names(HealthResource.Cpu));
    }

    [Fact]
    public void A_Process_That_Has_Gone_Is_Not_Named()
    {
        finder.Update(Processes(Entry("miner", 1, cpu: 0.9)), Start);
        finder.Update(Processes(Entry("editor", 2, cpu: 0.1)), Start.AddSeconds(5));

        Assert.Equal(["editor"], Names(HealthResource.Cpu));
    }

    [Fact]
    public void A_Process_Under_Several_Resources_Is_Listed_Once()
    {
        finder.Update(Processes(Entry("miner", 1, cpu: 0.9, memory: 500 * Megabyte)), Start);

        HealthCulprit culprit = Assert.Single(finder.Find([HealthResource.Cpu, HealthResource.Memory]));

        Assert.Equal([HealthResource.Cpu, HealthResource.Memory], culprit.Resources);
        Assert.Contains("CPU", culprit.Detail);
        Assert.Contains("memory", culprit.Detail);
    }

    private static ProcessEntry Entry(string name, int pid, double cpu = 0.0, long memory = 0, double disk = 0.0) =>
        new() {
            ProcessName        = name,
            Pid                = pid,
            CpuTimePercent     = cpu,
            CommittedMemory    = memory,
            DiskBytesPerSecond = disk
        };

    [Fact]
    public void Growing_Memory_Is_Reported_Per_Minute()
    {
        finder.Update(Processes(Entry("leaky", 1, memory: 100 * Megabyte)), Start);
        finder.Update(Processes(Entry("leaky", 1, memory: 160 * Megabyte)), Start.AddMinutes(1));

        HealthCulprit culprit = Assert.Single(finder.Find([HealthResource.Memory]));

        Assert.Contains("+60 MB/min", culprit.Detail);
    }

    [Fact]
    public void Idle_Processes_Are_Not_Named()
    {
        finder.Update(Processes(Entry("idle", 1, cpu: 0.001)), Start);

        Assert.Empty(finder.Find([HealthResource.Cpu]));
    }

    // Regression test: a process holding 54 GB of unwritten commit has a tiny working set, so going
    // by the working set blamed whichever other programs happened to be largest.
    [Fact]
    public void Memory_Goes_By_What_A_Process_Holds_Not_Its_Working_Set()
    {
        ProcessEntry holder = Entry("holder", 1, memory: 54L * 1024 * Megabyte);
        ProcessEntry editor = Entry("editor", 2, memory: 2L * 1024 * Megabyte);

        holder.UsedMemory = 10 * Megabyte;
        editor.UsedMemory = 2L * 1024 * Megabyte;

        finder.Update(Processes(holder, editor), Start);

        HealthCulprit top = finder.Find([HealthResource.Memory])[0];

        Assert.Equal("holder", top.ProcessName);
        Assert.Contains("54.0 GB memory", top.Detail);
    }

    private string[] Names(HealthResource resource) =>
        finder.Find([resource]).Select(culprit => culprit.ProcessName).ToArray();

    [Fact]
    public void Network_Pressure_Names_Nobody_As_There_Is_No_Per_Process_Figure()
    {
        finder.Update(Processes(Entry("browser", 1, cpu: 0.9)), Start);

        Assert.Empty(finder.Find([HealthResource.Network]));
    }

    private static ProcessInfo Processes(params ProcessEntry[] entries)
    {
        ProcessInfo info = new();
        info.Metrics.Entries.AddRange(entries);
        return info;
    }

    [Fact]
    public void Processes_With_The_Same_Name_Are_Grouped_Under_The_Busiest_Pid()
    {
        finder.Update(Processes(
            Entry("chrome", 10, cpu: 0.1, memory: 100 * Megabyte),
            Entry("chrome", 11, cpu: 0.4, memory: 200 * Megabyte),
            Entry("chrome", 12, cpu: 0.2, memory: 300 * Megabyte)), Start);

        HealthCulprit culprit = Assert.Single(finder.Find([HealthResource.Cpu]));

        Assert.Equal(3, culprit.ProcessCount);
        Assert.Equal(11, culprit.Pid);
    }

    [Fact]
    public void Recent_Use_Outranks_Use_From_Earlier_In_The_Run()
    {
        finder.Update(Processes(Entry("startup-job", 1, cpu: 0.9), Entry("runaway", 2, cpu: 0.0)), Start);

        for (int seconds = 5; seconds <= 60; seconds += 5) {
            finder.Update(
                Processes(Entry("startup-job", 1, cpu: 0.0), Entry("runaway", 2, cpu: 0.8)),
                Start.AddSeconds(seconds));
        }

        Assert.Equal("runaway", Names(HealthResource.Cpu)[0]);
    }

    [Fact]
    public void Task_Monitor_Does_Not_Name_Itself()
    {
        finder.Update(Processes(Entry("taskmon", Environment.ProcessId, cpu: 0.2), Entry("miner", 1, cpu: 0.9)), Start);

        Assert.Equal(["miner"], Names(HealthResource.Cpu));
    }

    [Fact]
    public void The_List_Is_Capped()
    {
        ProcessEntry[] entries = Enumerable.Range(1, 20)
            .Select(pid => Entry($"process{pid}", pid, cpu: 0.5, memory: pid * Megabyte, disk: 5.0 * Megabyte))
            .ToArray();

        finder.Update(Processes(entries), Start);

        Assert.True(finder.Find([HealthResource.Cpu, HealthResource.Memory, HealthResource.Disk]).Count <= 5);
    }

    // Regression test: a process using a little of both CPU and disk was listed ahead of yes and dd,
    // the processes doing the work, for being behind two resources.
    [Fact]
    public void The_Top_User_Of_Each_Resource_Comes_Before_A_Lesser_User_Of_Both()
    {
        finder.Update(Processes(
            Entry("yes", 1, cpu: 9.0),
            Entry("dd", 2, disk: 100.0 * Megabyte),
            Entry("browser", 3, cpu: 3.0, disk: 30.0 * Megabyte)), Start);

        string[] names = finder.Find([HealthResource.Cpu, HealthResource.Disk])
            .Select(culprit => culprit.ProcessName)
            .ToArray();

        Assert.Equal(["yes", "dd", "browser"], names);
    }

    [Fact]
    public void Volume_Pressure_Names_Nobody_As_There_Is_No_Per_Volume_Figure()
    {
        finder.Update(Processes(Entry("logger", 1, disk: 80.0 * Megabyte)), Start);

        Assert.Empty(finder.Find([HealthResource.Volumes]));
    }
}
