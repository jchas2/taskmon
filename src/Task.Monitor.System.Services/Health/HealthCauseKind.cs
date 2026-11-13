namespace Task.Monitor.System.Services.Health;

public enum HealthCauseKind
{
    AppNotResponding,
    CommitCharge,
    CpuBusy,
    CpuPerformanceLimit,
    DiskActive,
    DiskLatency,
    DriverTime,
    DriveWarning,
    DriveWear,
    GpuHeatLimit,
    GpuMemoryFree,
    GpuPowerLimit,
    MemoryAvailable,
    MemoryPressure,
    NetworkDiscards,
    NetworkErrors,
    PagesIn,
    RunQueue,
    SensorNearLimit,
    SwapOut,
    ThermalPressure,
    VolumeFull,
    VramFree
}
