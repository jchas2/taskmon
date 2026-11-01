#if __APPLE__
using Task.Monitor.System.Services.Health;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class HealthServiceReportsTests : IDisposable
{
    private const string RealHeader      = "{\"app_name\":\"bash\",\"bug_type\":\"309\"}";
    private const string SimulatedHeader = "{\"is_simulated\":1,\"app_name\":\"taskmon\",\"bug_type\":\"309\"}";

    private readonly string folder = Directory.CreateTempSubdirectory("taskmon-reports").FullName;

    [Fact]
    public void A_Missing_Report_Is_Not_Simulated() =>
        Assert.False(HealthService.IsSimulatedReport(Path.Combine(folder, "gone.ips")));

    [Fact]
    public void A_Real_Crash_Is_Not_Simulated() =>
        Assert.False(HealthService.IsSimulatedReport(Report("bash-2026-11-01-180623.ips", RealHeader)));

    [Fact]
    public void A_Simulated_Report_Is_Found_From_Its_First_Line() =>
        Assert.True(HealthService.IsSimulatedReport(Report("taskmon-2026-10-26-142330.ips", SimulatedHeader)));

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void Only_Ips_Reports_Can_Be_Simulated() =>
        Assert.False(HealthService.IsSimulatedReport(Report("taskmon_2026-10-26-142330_Mac.crash", SimulatedHeader)));

    private string Report(string name, string header)
    {
        string path = Path.Combine(folder, name);

        File.WriteAllText(path, $"{header}\n{{\"procName\":\"test\"}}\n");
        return path;
    }
}
#endif
