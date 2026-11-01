using Task.Monitor.System.Services.Health;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class EventLogReportParserTests
{
    [Fact]
    public void A_Crash_Names_The_App_And_Module()
    {
        HealthReport report = EventLogReportParser.Parse(Event(
            "Application Error",
            1000,
            "<Data Name='AppName'>chrome.exe</Data><Data Name='ModuleName'>ntdll.dll</Data>"))!;

        Assert.Equal(HealthReportKind.AppCrash, report.Kind);
        Assert.Equal("chrome.exe, in ntdll.dll", report.Detail);
        Assert.Equal("chrome.exe", report.Source);
    }

    // Regression test: a real [Environment]::FailFast crash records the module as "unknown".
    [Fact]
    public void A_Crash_With_An_Unknown_Module_Just_Names_The_App()
    {
        HealthReport report = EventLogReportParser.Parse(Event(
            "Application Error",
            1000,
            "<Data Name='AppName'>powershell.exe</Data><Data Name='ModuleName'>unknown</Data>"))!;

        Assert.Equal("powershell.exe", report.Detail);
    }

    [Fact]
    public void A_Hang_Reads_The_App_From_Unnamed_Data()
    {
        HealthReport report = EventLogReportParser.Parse(Event(
            "Application Hang",
            1002,
            "<Data>notepad.exe</Data><Data>10.0</Data>"))!;

        Assert.Equal(HealthReportKind.AppHang, report.Kind);
        Assert.Equal("notepad.exe", report.Source);
    }

    [Theory]
    [InlineData("disk", 7, "Bad block")]
    [InlineData("disk", 51, "Error during a paging operation")]
    [InlineData("disk", 153, "Request retried")]
    [InlineData("stornvme", 129, "stornvme reset the device")]
    public void Disk_Events_Are_Described(string provider, int eventId, string detail)
    {
        HealthReport report = EventLogReportParser.Parse(Event(provider, eventId))!;

        Assert.Equal(HealthReportKind.DiskError, report.Kind);
        Assert.Equal(detail, report.Detail);
    }

    private static string Event(string provider, int eventId, string data = "") =>
        "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'>" +
        $"<System><Provider Name='{provider}'/><EventID>{eventId}</EventID>" +
        "<TimeCreated SystemTime='2026-11-01T02:34:12.5000000Z'/><Channel>System</Channel></System>" +
        $"<EventData>{data}</EventData></Event>";

    [Theory]
    [InlineData("Application Error", 1001)]
    [InlineData("Microsoft-Windows-Kernel-Power", 109)]
    [InlineData("disk", 11)]
    [InlineData("Service Control Manager", 7036)]
    public void Other_Events_Are_Ignored(string provider, int eventId)
    {
        Assert.Null(EventLogReportParser.Parse(Event(provider, eventId)));
    }

    [Theory]
    [InlineData("Microsoft-Windows-Resource-Exhaustion-Detector", 2004, HealthReportKind.LowMemory)]
    [InlineData("Microsoft-Windows-Kernel-Power", 41, HealthReportKind.UnexpectedRestart)]
    [InlineData("Microsoft-Windows-WHEA-Logger", 17, HealthReportKind.HardwareError)]
    public void System_Events_Map_Across(string provider, int eventId, HealthReportKind kind)
    {
        Assert.Equal(kind, EventLogReportParser.Parse(Event(provider, eventId))!.Kind);
    }

    [Fact]
    public void Text_That_Is_Not_Xml_Is_Ignored()
    {
        Assert.Null(EventLogReportParser.Parse("not xml"));
    }

    [Fact]
    public void The_Time_Is_Read_As_Utc()
    {
        HealthReport report = EventLogReportParser.Parse(Event("disk", 7))!;

        Assert.Equal(new DateTime(2026, 11, 1, 2, 34, 12, 500, DateTimeKind.Utc), report.TimeUtc);
        Assert.Equal(DateTimeKind.Utc, report.TimeUtc.Kind);
    }
}
