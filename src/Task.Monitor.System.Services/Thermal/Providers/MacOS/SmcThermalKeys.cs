#if __APPLE__
namespace Task.Monitor.System.Services.Thermal.Providers.MacOS;

// The key meanings are undocumented; Apple Silicon is grouped by prefix, Intel by the long-standing key names.
internal static class SmcThermalKeys
{
    public const string EfficiencyCores  = "E-Cores";
    public const string PerformanceCores = "P-Cores";

    private const double MaximumCelsius = 130;
    private const double MinimumCelsius = 5;

    public readonly record struct Group(ThermalComponent Component, string Cluster);

    private static readonly Group Ambient = new(ThermalComponent.Ambient, string.Empty);
    private static readonly Group Battery = new(ThermalComponent.Battery, string.Empty);
    private static readonly Group Cpu     = new(ThermalComponent.Cpu,     string.Empty);
    private static readonly Group Gpu     = new(ThermalComponent.Gpu,     string.Empty);

    public static Group? Classify(string key, bool appleSilicon)
    {
        if (key is ['T', 'B', >= '0' and <= '9', 'T']) {
            return Battery;
        }

        return appleSilicon ? ClassifyAppleSilicon(key) : ClassifyIntel(key);
    }

    private static Group? ClassifyAppleSilicon(string key) => key switch {
        ['T', 'p', _, _] => new Group(ThermalComponent.Cpu, PerformanceCores),
        ['T', 'e', _, _] => new Group(ThermalComponent.Cpu, EfficiencyCores),
        ['T', 'g', _, _] => Gpu,
        ['T', 'a', _, _] => Ambient,
                       _ => null
    };

    private static Group? ClassifyIntel(string key) => key switch {
        "TCXC"                                                  => Cpu,
        ['T', 'C', >= '0' and <= '9', 'C' or 'D' or 'E' or 'F' or 'P'] => Cpu,
        ['T', 'G', '0', 'D' or 'H' or 'P']                     => Gpu,
        ['T', 'A', >= '0' and <= '9', 'P']                     => Ambient,
                                                              _ => null
    };

    public static IEnumerable<(Group Group, double Celsius)> Hottest(IEnumerable<(Group Group, double Celsius)> readings) =>
        readings
            .Where(reading => reading.Celsius is >= MinimumCelsius and <= MaximumCelsius)
            .GroupBy(reading => reading.Group)
            .Select(group => (group.Key, group.Max(reading => reading.Celsius)));
}
#endif
