using Task.Monitor.System.Services.Health;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class DiagnosticReportParserTests
{
    private static readonly DateTime Time = new(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_Report_With_An_Unusual_Name_Still_Names_Something()
    {
        Assert.Equal("oddname", DiagnosticReportParser.Parse("oddname.ips", Time)!.Source);
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
    [InlineData("Safari-2026-11-01-120000.ips", HealthReportKind.AppCrash, "Safari")]
    [InlineData("Safari_2026-11-01-120000_MacBook.crash", HealthReportKind.AppCrash, "Safari")]
    [InlineData("Google Chrome_2026-11-01-120000_MacBook.hang", HealthReportKind.AppHang, "Google Chrome")]
    [InlineData("Xcode_2026-11-01-120000_MacBook.spin", HealthReportKind.AppHang, "Xcode")]
    [InlineData("backupd_2026-11-01-120000_MacBook.cpu_resource.diag", HealthReportKind.ResourceLimit, "backupd")]
    [InlineData("mds_2026-11-01-120000_MacBook.diskwrites_resource.diag", HealthReportKind.ResourceLimit, "mds")]
    public void Reports_Are_Read_From_The_File_Name(string fileName, HealthReportKind kind, string app)
    {
        HealthReport report = DiagnosticReportParser.Parse(fileName, Time)!;

        Assert.Equal(kind, report.Kind);
        Assert.Equal(app, report.Source);
        Assert.Equal(Time, report.TimeUtc);
    }
}
