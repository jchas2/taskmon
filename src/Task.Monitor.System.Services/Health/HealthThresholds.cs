using System.Diagnostics;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.System.Services.Health;

public sealed record HealthThresholds
{
    private const int MaxPercent = 100;

    public int             ClearSeconds                   { get; init; } = 30;
    public int             CommitCriticalPercent          { get; init; } = 97;
    public int             CommitWarningPercent           { get; init; } = 90;
    public int             CpuCriticalPercent             { get; init; } = 98;
    public int             CpuWarningPercent              { get; init; } = 90;
    public int             CriticalSustainSeconds         { get; init; } = 60;
    public int             DiskActiveWarningPercent       { get; init; } = 90;
    public int             DiskLatencyCriticalMs          { get; init; } = 100;
    public int             DiskLatencyWarningMs           { get; init; } = 20;
    public int             DriverTimeWarningPercent       { get; init; } = 15;
    public int             EventHistorySize               { get; init; } = 50;
    public int             GpuMemoryFreeWarningPercent    { get; init; } = 10;
    public int             HardFaultsAvailablePercent     { get; init; } = 25;
    public int             HardFaultsWarningPerSecond     { get; init; } = 500;
    public int             MemoryAvailableCriticalPercent { get; init; } = 5;
    public int             MemoryAvailableWarningPercent  { get; init; } = 10;

    private static int NonNegative(string name, int value, int defaultValue)
    {
        if (value >= 0) {
            return value;
        }

        Trace.WriteLine($"[health] {name}={value} is below zero, using {defaultValue}.");
        return defaultValue;
    }

    public int             NotRespondingCriticalSeconds   { get; init; } = 30;
    public int             NotRespondingWarningSeconds    { get; init; } = 5;
    public int             NvmeWearWarningPercent         { get; init; } = 90;

    private static (int Low, int High) Ordered(
        string name, 
        int    low, 
        int    high, 
        int    defaultLow, 
        int    defaultHigh, 
        int    max = MaxPercent)
    {
        if (low >= 0 && low <= high && high <= max) {
            return (low, high);
        }

        Trace.WriteLine($"[health] {name}: {low} and {high} are out of order, using {defaultLow} and {defaultHigh}.");
        return (defaultLow, defaultHigh);
    }

    private static int Percent(string name, int value, int defaultValue)
    {
        if (value is >= 0 and <= MaxPercent) {
            return value;
        }

        Trace.WriteLine($"[health] {name}={value} is not a percentage, using {defaultValue}.");
        return defaultValue;
    }

    public int             PerformanceLimitWarningPercent { get; init; } = 80;
    public int             ReportLookbackHours            { get; init; } = 24;
    public int             RunQueueBusyPercent            { get; init; } = 50;
    public int             RunQueueWarningPerCore         { get; init; } = 2;
    public int             SensorCriticalMarginCelsius    { get; init; } = 3;
    public int             SensorWarningMarginCelsius     { get; init; } = 10;
    public int             SustainGraceSeconds            { get; init; } = 5;
    public int             SwapOutWarningMbPerSecond      { get; init; } = 0;
    public int             SystemVolumeFreeCriticalGb     { get; init; } = 2;
    public ThermalPressure ThermalPressureCritical        { get; init; } = ThermalPressure.Heavy;
    public ThermalPressure ThermalPressureWarning         { get; init; } = ThermalPressure.Moderate;

    public HealthThresholds Validated()
    {
        HealthThresholds defaults = new();

        (int cpuWarning, int cpuCritical) = Ordered(
            nameof(CpuWarningPercent),
            CpuWarningPercent, CpuCriticalPercent,
            defaults.CpuWarningPercent, defaults.CpuCriticalPercent);

        (int commitWarning, int commitCritical) = Ordered(
            nameof(CommitWarningPercent),
            CommitWarningPercent, CommitCriticalPercent,
            defaults.CommitWarningPercent, defaults.CommitCriticalPercent);

        (int memoryCritical, int memoryWarning) = Ordered(
            nameof(MemoryAvailableWarningPercent),
            MemoryAvailableCriticalPercent, MemoryAvailableWarningPercent,
            defaults.MemoryAvailableCriticalPercent, defaults.MemoryAvailableWarningPercent);

        (int volumeCritical, int volumeWarning) = Ordered(
            nameof(VolumeFreeWarningPercent),
            VolumeFreeCriticalPercent, VolumeFreeWarningPercent,
            defaults.VolumeFreeCriticalPercent, defaults.VolumeFreeWarningPercent);

        (int sensorCritical, int sensorWarning) = Ordered(
            nameof(SensorWarningMarginCelsius),
            SensorCriticalMarginCelsius, SensorWarningMarginCelsius,
            defaults.SensorCriticalMarginCelsius, defaults.SensorWarningMarginCelsius);

        (int latencyWarning, int latencyCritical) = Ordered(
            nameof(DiskLatencyWarningMs),
            DiskLatencyWarningMs, DiskLatencyCriticalMs,
            defaults.DiskLatencyWarningMs, defaults.DiskLatencyCriticalMs,
            int.MaxValue);

        (int hangWarning, int hangCritical) = Ordered(
            nameof(NotRespondingWarningSeconds),
            NotRespondingWarningSeconds, NotRespondingCriticalSeconds,
            defaults.NotRespondingWarningSeconds, defaults.NotRespondingCriticalSeconds,
            int.MaxValue);

        bool pressureInOrder = ThermalPressureWarning <= ThermalPressureCritical;

        if (!pressureInOrder) {
            Trace.WriteLine($"[health] {nameof(ThermalPressureWarning)} is above {nameof(ThermalPressureCritical)}, using the defaults.");
        }

        return this with {
            ClearSeconds                   = NonNegative(nameof(ClearSeconds), ClearSeconds, defaults.ClearSeconds),
            CommitCriticalPercent          = commitCritical,
            CommitWarningPercent           = commitWarning,
            CpuCriticalPercent             = cpuCritical,
            CpuWarningPercent              = cpuWarning,
            CriticalSustainSeconds         = NonNegative(nameof(CriticalSustainSeconds), CriticalSustainSeconds, defaults.CriticalSustainSeconds),
            DiskActiveWarningPercent       = Percent(nameof(DiskActiveWarningPercent), DiskActiveWarningPercent, defaults.DiskActiveWarningPercent),
            DiskLatencyCriticalMs          = latencyCritical,
            DiskLatencyWarningMs           = latencyWarning,
            DriverTimeWarningPercent       = Percent(nameof(DriverTimeWarningPercent), DriverTimeWarningPercent, defaults.DriverTimeWarningPercent),
            EventHistorySize               = NonNegative(nameof(EventHistorySize), EventHistorySize, defaults.EventHistorySize),
            GpuMemoryFreeWarningPercent    = Percent(nameof(GpuMemoryFreeWarningPercent), GpuMemoryFreeWarningPercent, defaults.GpuMemoryFreeWarningPercent),
            HardFaultsAvailablePercent     = Percent(nameof(HardFaultsAvailablePercent), HardFaultsAvailablePercent, defaults.HardFaultsAvailablePercent),
            HardFaultsWarningPerSecond     = NonNegative(nameof(HardFaultsWarningPerSecond), HardFaultsWarningPerSecond, defaults.HardFaultsWarningPerSecond),
            MemoryAvailableCriticalPercent = memoryCritical,
            MemoryAvailableWarningPercent  = memoryWarning,
            NotRespondingCriticalSeconds   = hangCritical,
            NotRespondingWarningSeconds    = hangWarning,
            NvmeWearWarningPercent         = Percent(nameof(NvmeWearWarningPercent), NvmeWearWarningPercent, defaults.NvmeWearWarningPercent),
            PerformanceLimitWarningPercent = Percent(nameof(PerformanceLimitWarningPercent), PerformanceLimitWarningPercent, defaults.PerformanceLimitWarningPercent),
            ReportLookbackHours            = NonNegative(nameof(ReportLookbackHours), ReportLookbackHours, defaults.ReportLookbackHours),
            RunQueueBusyPercent            = Percent(nameof(RunQueueBusyPercent), RunQueueBusyPercent, defaults.RunQueueBusyPercent),
            RunQueueWarningPerCore         = NonNegative(nameof(RunQueueWarningPerCore), RunQueueWarningPerCore, defaults.RunQueueWarningPerCore),
            SensorCriticalMarginCelsius    = sensorCritical,
            SensorWarningMarginCelsius     = sensorWarning,
            SustainGraceSeconds            = NonNegative(nameof(SustainGraceSeconds), SustainGraceSeconds, defaults.SustainGraceSeconds),
            SwapOutWarningMbPerSecond      = NonNegative(nameof(SwapOutWarningMbPerSecond), SwapOutWarningMbPerSecond, defaults.SwapOutWarningMbPerSecond),
            SystemVolumeFreeCriticalGb     = NonNegative(nameof(SystemVolumeFreeCriticalGb), SystemVolumeFreeCriticalGb, defaults.SystemVolumeFreeCriticalGb),
            ThermalPressureCritical        = pressureInOrder ? ThermalPressureCritical : defaults.ThermalPressureCritical,
            ThermalPressureWarning         = pressureInOrder ? ThermalPressureWarning : defaults.ThermalPressureWarning,
            VolumeFreeCriticalPercent      = volumeCritical,
            VolumeFreeWarningPercent       = volumeWarning,
            WarningSustainSeconds          = NonNegative(nameof(WarningSustainSeconds), WarningSustainSeconds, defaults.WarningSustainSeconds)
        };
    }

    public int             VolumeFreeCriticalPercent      { get; init; } = 5;
    public int             VolumeFreeWarningPercent       { get; init; } = 10;
    public int             WarningSustainSeconds          { get; init; } = 30;
}
