using Task.Monitor.System.Services.Health;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class DiagnosticReportParserTests
{
    private static readonly DateTime Time = new(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_Jetsam_Report_Is_Low_Memory_Rather_Than_An_App_Crash()
    {
        HealthReport report = DiagnosticReportParser.Parse("JetsamEvent-2026-10-25-073804.ips", Time)!;

        Assert.Equal(HealthReportCause.JetsamReport, report.Cause);
        Assert.Equal(HealthReportKind.LowMemory, report.Kind);
        Assert.Equal(string.Empty, report.App);
    }

    [Theory]
    [InlineData("disk writes", HealthReportCause.DiskWriteLimitReport)]
    [InlineData("cpu usage", HealthReportCause.CpuLimitReport)]
    public void A_Plain_Diag_Report_Is_Read_From_Its_Event(string diagEvent, HealthReportCause cause)
    {
        HealthReport report = DiagnosticReportParser.Parse("dd_2026-11-01-161053_MacBook.diag", Time, diagEvent)!;

        Assert.Equal(HealthReportKind.ResourceLimit, report.Kind);
        Assert.Equal(cause, report.Cause);
        Assert.Equal("dd", report.App);
    }

    [Fact]
    public void A_Plain_Diag_Report_With_Another_Event_Is_Ignored()
    {
        Assert.Null(DiagnosticReportParser.Parse("dd_2026-11-01-161053_MacBook.diag", Time, "wakeups"));
    }

    // Regression test: macOS logged this when taskmon read kernel_task's memory map. taskmon kept
    // running, but the report read as a crash under Recent events.
    [Fact]
    public void A_Report_Marked_As_Simulated_Is_Recognised()
    {
        Assert.True(DiagnosticReportParser.IsSimulated("{\"is_simulated\":1,\"app_name\":\"taskmon\",\"bug_type\":\"309\"}"));
    }

    [Theory]
    [InlineData("{\"app_name\":\"bash\",\"bug_type\":\"309\"}")]
    [InlineData("{\"is_simulated\":0,\"app_name\":\"bash\"}")]
    [InlineData("")]
    [InlineData(null)]
    public void A_Report_Not_Marked_As_Simulated_Is_Not(string? header)
    {
        Assert.False(DiagnosticReportParser.IsSimulated(header));
    }

    [Fact]
    public void A_Report_With_An_Unusual_Name_Still_Names_Something()
    {
        Assert.Equal("oddname", DiagnosticReportParser.Parse("oddname.ips", Time)!.App);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("Retired")]
    [InlineData("shutdown_stall_2026-11-01-120000_MacBook.shutdownStall")]
    public void Other_Files_Are_Ignored(string fileName)
    {
        Assert.Null(DiagnosticReportParser.Parse(fileName, Time));
    }

    [Theory]
    [InlineData("Safari-2026-11-01-120000.ips", HealthReportCause.CrashReport, HealthReportKind.AppCrash, "Safari")]
    [InlineData("Safari_2026-11-01-120000_MacBook.crash", HealthReportCause.CrashReport, HealthReportKind.AppCrash, "Safari")]
    [InlineData("Google Chrome_2026-11-01-120000_MacBook.hang", HealthReportCause.HangReport, HealthReportKind.AppHang, "Google Chrome")]
    [InlineData("Xcode_2026-11-01-120000_MacBook.spin", HealthReportCause.SpinReport, HealthReportKind.AppHang, "Xcode")]
    [InlineData("backupd_2026-11-01-120000_MacBook.cpu_resource.diag", HealthReportCause.CpuLimitReport, HealthReportKind.ResourceLimit, "backupd")]
    [InlineData("mds_2026-11-01-120000_MacBook.diskwrites_resource.diag", HealthReportCause.DiskWriteLimitReport, HealthReportKind.ResourceLimit, "mds")]
    public void Reports_Are_Read_From_The_File_Name(string fileName, HealthReportCause cause, HealthReportKind kind, string app)
    {
        HealthReport report = DiagnosticReportParser.Parse(fileName, Time)!;

        Assert.Equal(cause, report.Cause);
        Assert.Equal(kind, report.Kind);
        Assert.Equal(app, report.App);
        Assert.Equal(Time, report.TimeUtc);
    }
}
