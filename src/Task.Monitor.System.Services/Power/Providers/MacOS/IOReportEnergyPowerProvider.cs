#if __APPLE__
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Power.Providers.MacOS;

internal sealed class IOReportEnergyPowerProvider(
    IOReportEnergySampler          sampler,
    Func<IReadOnlyList<GpuDevice>> getGpus) : IPowerProvider
{
    private static readonly (PowerComponent Component, string Rail)[] ReadingOrder = [
        (PowerComponent.Cpu,          PowerRails.Cpu),
        (PowerComponent.Cpu,          PowerRails.EfficiencyCores),
        (PowerComponent.Cpu,          PowerRails.PerformanceCores),
        (PowerComponent.Gpu,          PowerRails.Gpu),
        (PowerComponent.NeuralEngine, PowerRails.NeuralEngine),
        (PowerComponent.Memory,       PowerRails.Dram),
        (PowerComponent.Display,      PowerRails.Display),
    ];

    public void Dispose() { }

    private static bool IsCluster(string channel, string prefix) =>
        channel.StartsWith(prefix, StringComparison.Ordinal) && channel.EndsWith("_CPU", StringComparison.Ordinal);

    internal static (PowerComponent Component, string Rail)? MapChannel(string channel) => channel switch {
        "CPU Energy"                      => (PowerComponent.Cpu,          PowerRails.Cpu),
        "ECPU"                            => (PowerComponent.Cpu,          PowerRails.EfficiencyCores),
        "PCPU"                            => (PowerComponent.Cpu,          PowerRails.PerformanceCores),
        "GPU Energy"                      => (PowerComponent.Gpu,          PowerRails.Gpu),
        "ANE"                             => (PowerComponent.NeuralEngine, PowerRails.NeuralEngine),
        "DRAM"                            => (PowerComponent.Memory,       PowerRails.Dram),
        "DISP" or "DISPEXT"               => (PowerComponent.Display,      PowerRails.Display),
        _ when IsCluster(channel, "EACC") => (PowerComponent.Cpu,          PowerRails.EfficiencyCores),
        _ when IsCluster(channel, "PACC") => (PowerComponent.Cpu,          PowerRails.PerformanceCores),
                                        _ => null
    };

    public string Name => "IOReport";

    public IEnumerable<PowerReading> Read()
    {
        if (sampler.Sample(out double seconds) is not { } deltas) {
            return [];
        }

        string gpuId = getGpus().FirstOrDefault()?.AdapterLuid.ToString() ?? "0";

        return ToReadings(deltas, seconds, gpuId);
    }

    internal static double? ToJoules(long value, string? unit) => unit switch {
        "J"          => value,
        "mJ"         => value * 1e-3,
        "uJ" or "µJ" => value * 1e-6,
        "nJ"         => value * 1e-9,
                   _ => null
    };

    internal static List<PowerReading> ToReadings(IEnumerable<IOReportEnergySampler.Delta> deltas, double seconds, string gpuId)
    {
        List<PowerReading> readings = new();

        if (seconds <= 0) {
            return readings;
        }

        Dictionary<(PowerComponent, string), double> joules = new();

        foreach (IOReportEnergySampler.Delta delta in deltas) {
            if (MapChannel(delta.Channel) is { } key && ToJoules(delta.Value, delta.Unit) is { } energy) {
                joules[key] = joules.GetValueOrDefault(key) + energy;
            }
        }

        (PowerComponent, string) cpuTotal = (PowerComponent.Cpu, PowerRails.Cpu);

        if (!joules.ContainsKey(cpuTotal)) {
            double clusters = joules.Where(entry => entry.Key.Item1 == PowerComponent.Cpu).Sum(entry => entry.Value);

            if (clusters > 0) {
                joules[cpuTotal] = clusters;
            }
        }

        foreach ((PowerComponent component, string rail) in ReadingOrder) {
            if (!joules.TryGetValue((component, rail), out double energy)) {
                continue;
            }

            readings.Add(new PowerReading {
                Component   = component,
                ComponentId = component == PowerComponent.Gpu ? gpuId : string.Empty,
                Rail        = rail,
                Watts       = Math.Max(0, energy / seconds),
                Source      = PowerSource.IOReport
            });
        }

        return readings;
    }

    public bool TryInitialise() => sampler.EnsureSubscribed();
}
#endif
