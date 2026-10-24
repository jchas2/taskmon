#if __APPLE__
using System.Buffers.Binary;

namespace Task.Monitor.System.Services.Soc;

internal static class SocMetricsCalculator
{
    private const string GpuStatsGroup           = "GPU Stats";
    private const string GpuPerformanceStates    = "GPU Performance States";
    private const string GpuThermalLimitStates   = "CLTM-induced GPU Performance States";
    private const string GpuPowerControllerState = "GPU Power Controller States";
    private const string NeuralEngineGroup       = "ANE";
    private const string NeuralEngineRunning     = "Running";

    private static readonly HashSet<string> GpuIdleControllerStates  = ["IDLE_OFF", "STANDBY"];
    private static readonly HashSet<string> GpuPowerControllerLimits = ["POWER", "PWR_ZONE", "PPMLIMIT"];

    public static SocMetrics Calculate(IReadOnlyList<IOReportChannelSample> channels, IReadOnlyList<double> gpuFrequencies)
    {
        SocMetrics metrics = new() {
            GpuMaxFrequencyMhz = gpuFrequencies.Count > 0 ? gpuFrequencies.Max() : null
        };

        foreach (IOReportChannelSample channel in channels) {
            if (channel.Group != GpuStatsGroup) {
                continue;
            }

            switch (channel.SubGroup) {
                case GpuPerformanceStates:
                    metrics.GpuFrequencyMhz = WeightedFrequencyMhz(channel.States, gpuFrequencies);
                    break;
                case GpuThermalLimitStates:
                    metrics.GpuThermalLimitRatio = Share(channel.States, state => TryParsePerformanceState(state, out _));
                    break;
                case GpuPowerControllerState:
                    metrics.GpuPowerLimitRatio = Share(
                        channel.States,
                        GpuPowerControllerLimits.Contains,
                        GpuIdleControllerStates.Contains);
                    break;
            }
        }

        return metrics;
    }

    public static double? NeuralEngineActiveRatio(IReadOnlyList<IOReportChannelSample> channels) =>
        channels.FirstOrDefault(channel => channel.Group == NeuralEngineGroup) is { } channel
            ? Share(channel.States, state => state == NeuralEngineRunning)
            : null;

    // The voltage-state tables hold 8-byte entries whose first word is the frequency, in Hz on
    // M1-M3 and in kHz on later chips.
    public static List<double> ParseFrequencyTable(ReadOnlySpan<byte> data)
    {
        List<double> raw = new();

        for (int offset = 0; offset + 8 <= data.Length; offset += 8) {
            raw.Add(BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]));
        }

        double toMhz = raw.Count > 0 && raw.Max() < 10_000_000 ? 1e-3 : 1e-6;

        return raw.Select(value => value * toMhz).ToList();
    }

    public static double? Share(
        IReadOnlyList<(string Name, long Residency)> states,
        Func<string, bool> isMatch,
        Func<string, bool>? isExcluded = null)
    {
        long total   = 0;
        long matched = 0;

        foreach ((string name, long residency) in states) {
            if (residency <= 0 || isExcluded?.Invoke(name) == true) {
                continue;
            }

            total += residency;

            if (isMatch(name)) {
                matched += residency;
            }
        }

        return total > 0 ? (double)matched / total : null;
    }

    private static bool TryParsePerformanceState(string state, out int index)
    {
        index = 0;
        return state.Length > 1 && state[0] == 'P' && int.TryParse(state.AsSpan(1), out index);
    }

    // State Pn runs at entry n of the frequency table; OFF and anything unknown is left out, so
    // this is the average speed while the GPU is running.
    public static double? WeightedFrequencyMhz(
        IReadOnlyList<(string Name, long Residency)> states,
        IReadOnlyList<double> frequencies)
    {
        double weighted = 0;
        long   active   = 0;

        foreach ((string name, long residency) in states) {
            if (residency <= 0 || !TryParsePerformanceState(name, out int index) || index >= frequencies.Count) {
                continue;
            }

            weighted += frequencies[index] * residency;
            active   += residency;
        }

        return active > 0 ? weighted / active : null;
    }
}
#endif
