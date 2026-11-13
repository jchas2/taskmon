namespace Task.Monitor.System.Services.Health;

public enum HealthReportCause
{
    // Windows event log records.
    AppError,
    AppHang,
    BadBlock,
    DeviceReset,
    HardwareError,
    PagingError,
    RequestRetried,
    UnexpectedRestart,
    VirtualMemoryLow,

    // macOS diagnostic reports.
    CpuLimitReport,
    CrashReport,
    DiskWriteLimitReport,
    HangReport,
    JetsamReport,
    SpinReport
}
