namespace Task.Monitor.System.Services.Health;

public enum HealthReportKind
{
    AppCrash,
    AppHang,
    DiskError,
    HardwareError,
    LowMemory,
    ResourceLimit,
    UnexpectedRestart
}
