using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Health;

internal sealed class HealthCulpritFinder
{
    private const double BytesPerMegabyte = 1024.0 * 1024.0;
    private const int    MaxCulprits      = 5;
    private const double MinCpuRatio      = 0.01;
    private const double MinDiskBytes     = BytesPerMegabyte;
    private const double MinGpuRatio      = 0.01;
    private const double MinShareOfTop    = 0.25;
    private const int    PerResource      = 3;
    private const double SecondsPerMinute = 60.0;
    private const double SmoothingSeconds = 20.0;
    private const double PercentPerRatio  = 100.0;

    private readonly Dictionary<string, ProcessTrend> trends = new();

    private DateTime? lastUpdateUtc;

    private sealed class ProcessTrend
    {
        public double   Cpu;
        public double   Disk;
        public double   Gpu;
        public long     Memory;
        public int      Count;
        public int      Pid;
        public double   TopCpu;
        public long     BaselineMemory;
        public DateTime BaselineUtc;
        public double   MemoryGrowthPerMinute;
        public bool     IsLive;
    }

    private static HealthCulprit Culprit(string name, ProcessTrend trend, IReadOnlyList<HealthResource> resources)
    {
        bool isThermal = resources.Contains(HealthResource.Thermal);
        bool isMemory  = resources.Contains(HealthResource.Memory);
        bool isCpu     = resources.Contains(HealthResource.Cpu) || (isThermal && trend.Cpu >= MinCpuRatio);
        bool isGpu     = resources.Contains(HealthResource.Gpu) || (isThermal && trend.Gpu >= MinGpuRatio);
        bool isGrowing = isMemory && trend.MemoryGrowthPerMinute >= BytesPerMegabyte;

        return new HealthCulprit {
            ProcessName                = name,
            ProcessCount               = trend.Count,
            Pid                        = trend.Pid,
            Resources                  = resources,
            CpuPercent                 = isCpu ? trend.Cpu * PercentPerRatio : null,
            MemoryBytes                = isMemory ? trend.Memory : null,
            MemoryGrowthBytesPerMinute = isGrowing ? trend.MemoryGrowthPerMinute : null,
            DiskBytesPerSecond         = resources.Contains(HealthResource.Disk) ? trend.Disk : null,
            GpuPercent                 = isGpu ? trend.Gpu * PercentPerRatio : null
        };
    }

    public IReadOnlyList<HealthCulprit> Find(IEnumerable<HealthResource> underPressure)
    {
        Dictionary<string, List<HealthResource>> found = new();
        Dictionary<string, double> shares = new();

        foreach (HealthResource resource in underPressure) {
            List<(string Name, double Usage)> top = trends
                .Where(entry => IsUsing(entry.Value, resource))
                .OrderByDescending(entry => Usage(entry.Value, resource))
                .Take(PerResource)
                .Select(entry => (entry.Key, Usage(entry.Value, resource)))
                .ToList();

            foreach ((string name, double usage) in top) {
                double share = usage / top[0].Usage;

                if (share < MinShareOfTop) {
                    continue;
                }

                if (!found.TryGetValue(name, out List<HealthResource>? resources)) {
                    resources = found[name] = new();
                }

                resources.Add(resource);
                shares[name] = Math.Max(shares.GetValueOrDefault(name), share);
            }
        }

        return found
            .OrderByDescending(entry => shares[entry.Key])
            .ThenByDescending(entry => entry.Value.Count)
            .Take(MaxCulprits)
            .Select(entry => Culprit(entry.Key, trends[entry.Key], entry.Value))
            .ToList();
    }

    private static bool IsUsing(ProcessTrend trend, HealthResource resource) => resource switch {
        HealthResource.Cpu     => trend.Cpu >= MinCpuRatio,
        HealthResource.Memory  => trend.Memory > 0,
        HealthResource.Disk    => trend.Disk >= MinDiskBytes,
        HealthResource.Gpu     => trend.Gpu >= MinGpuRatio,
        HealthResource.Thermal => trend.Cpu >= MinCpuRatio || trend.Gpu >= MinGpuRatio,
                             _ => false
    };

    private static void Merge(ProcessEntry entry, Dictionary<string, (double Cpu, double Gpu, double Disk, long Memory)> totals)
    {
        totals.TryGetValue(entry.ProcessName, out (double Cpu, double Gpu, double Disk, long Memory) total);

        totals[entry.ProcessName] = (
            total.Cpu + entry.CpuTimePercent,
            total.Gpu + entry.GpuTimePercent,
            total.Disk + entry.DiskBytesPerSecond,
            total.Memory + entry.CommittedMemory);
    }

    public void Update(ProcessInfo? processes, DateTime utcNow)
    {
        if (processes == null) {
            return;
        }

        double elapsed = lastUpdateUtc is { } last ? (utcNow - last).TotalSeconds : SmoothingSeconds;
        double weight  = 1.0 - Math.Exp(-Math.Max(0.0, elapsed) / SmoothingSeconds);

        lastUpdateUtc = utcNow;

        foreach (ProcessTrend trend in trends.Values) {
            trend.IsLive = false;
            trend.Count  = 0;
            trend.TopCpu = -1.0;
        }

        Dictionary<string, (double Cpu, double Gpu, double Disk, long Memory)> totals = new();

        foreach (ProcessEntry entry in processes.Metrics.Entries) {
            if (entry.Pid == Environment.ProcessId) {
                continue;
            }

            Merge(entry, totals);

            if (!trends.TryGetValue(entry.ProcessName, out ProcessTrend? trend)) {
                trend = trends[entry.ProcessName] = new ProcessTrend {
                    BaselineMemory = 0,
                    BaselineUtc    = utcNow
                };
            }

            trend.IsLive = true;
            trend.Count++;

            if (entry.CpuTimePercent > trend.TopCpu) {
                trend.TopCpu = entry.CpuTimePercent;
                trend.Pid    = entry.Pid;
            }
        }

        foreach ((string name, (double cpu, double gpu, double disk, long memory)) in totals) {
            UpdateTrend(trends[name], cpu, gpu, disk, memory, weight, utcNow);
        }

        foreach (string gone in trends.Where(entry => !entry.Value.IsLive).Select(entry => entry.Key).ToList()) {
            trends.Remove(gone);
        }
    }

    private static void UpdateTrend(
        ProcessTrend trend,
        double       cpu,
        double       gpu,
        double       disk,
        long         memory,
        double       weight,
        DateTime     utcNow)
    {
        trend.Cpu   += (cpu - trend.Cpu) * weight;
        trend.Gpu   += (gpu - trend.Gpu) * weight;
        trend.Disk  += (disk - trend.Disk) * weight;
        trend.Memory = memory;

        if (trend.BaselineMemory == 0) {
            trend.BaselineMemory = memory;
            trend.BaselineUtc    = utcNow;
            return;
        }

        double minutes = (utcNow - trend.BaselineUtc).TotalSeconds / SecondsPerMinute;

        if (minutes >= 1.0) {
            trend.MemoryGrowthPerMinute = (memory - trend.BaselineMemory) / minutes;
            trend.BaselineMemory        = memory;
            trend.BaselineUtc           = utcNow;
        }
    }

    private static double Usage(ProcessTrend trend, HealthResource resource) => resource switch {
        HealthResource.Cpu     => trend.Cpu,
        HealthResource.Memory  => trend.Memory,
        HealthResource.Disk    => trend.Disk,
        HealthResource.Gpu     => trend.Gpu,
        HealthResource.Thermal => trend.Cpu + trend.Gpu,
                             _ => 0.0
    };
}
