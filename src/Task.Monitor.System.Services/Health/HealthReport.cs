namespace Task.Monitor.System.Services.Health;

public sealed record HealthReport
{
    public required DateTime          TimeUtc { get; init; }
    public required HealthReportCause Cause   { get; init; }
    public string                     App     { get; init; } = string.Empty;
    public string                     Module  { get; init; } = string.Empty;
    public string                     Driver  { get; init; } = string.Empty;
    public int                        EventId { get; init; }

    // The causes not listed are the disk errors: a bad block, a paging error, a device reset and a
    // retried request.
    public HealthReportKind Kind => Cause switch {
        HealthReportCause.AppError             => HealthReportKind.AppCrash,
        HealthReportCause.CrashReport          => HealthReportKind.AppCrash,
        HealthReportCause.AppHang              => HealthReportKind.AppHang,
        HealthReportCause.HangReport           => HealthReportKind.AppHang,
        HealthReportCause.SpinReport           => HealthReportKind.AppHang,
        HealthReportCause.HardwareError        => HealthReportKind.HardwareError,
        HealthReportCause.VirtualMemoryLow     => HealthReportKind.LowMemory,
        HealthReportCause.JetsamReport         => HealthReportKind.LowMemory,
        HealthReportCause.CpuLimitReport       => HealthReportKind.ResourceLimit,
        HealthReportCause.DiskWriteLimitReport => HealthReportKind.ResourceLimit,
        HealthReportCause.UnexpectedRestart    => HealthReportKind.UnexpectedRestart,
                                             _ => HealthReportKind.DiskError
    };
}
