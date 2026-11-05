#if __WIN32__
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services.Tests.WindowsServices;

// Only services that don't exist are started or stopped here: run elevated, a real one would be.
public sealed class WindowsServicesServiceTests
{
    private const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;

    private static string MissingServiceName() => $"taskmon-test-missing-{Guid.NewGuid():N}";

    [Fact]
    public void RequestControl_Reports_A_Missing_Service_From_The_Worker()
    {
        WindowsServicesService service = new() { Delay = WorkerService.MinimumDelayInMilliseconds };
        using ManualResetEventSlim done = new(initialState: false);
        ServiceControlOutcome?     outcome = null;

        service.Start();

        try {
            service.RequestControl(MissingServiceName(), start: true, result => {
                outcome = result;
                done.Set();
            });

            Assert.True(done.Wait(TimeSpan.FromSeconds(30)), "the worker should answer the request");
        }
        finally {
            service.Stop();
        }

        Assert.Equal(ServiceControlResult.NotFound, outcome?.Result);
        Assert.Equal(ERROR_SERVICE_DOES_NOT_EXIST, outcome?.Win32Error);
    }

    [Fact]
    public void RunningDependents_Of_A_Missing_Service_Is_Empty() =>
        Assert.Empty(ServiceControlManager.RunningDependents(MissingServiceName()));

    [Fact]
    public void StartService_And_StopService_Return_The_Win32_Error_For_A_Missing_Service()
    {
        Assert.Equal(ERROR_SERVICE_DOES_NOT_EXIST, ServiceControlManager.StartService(MissingServiceName()));
        Assert.Equal(ERROR_SERVICE_DOES_NOT_EXIST, ServiceControlManager.StopService(MissingServiceName()));
    }

    [Theory]
    [InlineData(0,    ServiceControlResult.Succeeded)]
    [InlineData(1056, ServiceControlResult.Succeeded)]
    [InlineData(1062, ServiceControlResult.Succeeded)]
    [InlineData(5,    ServiceControlResult.AccessDenied)]
    [InlineData(1058, ServiceControlResult.Disabled)]
    [InlineData(1051, ServiceControlResult.DependentsRunning)]
    [InlineData(1068, ServiceControlResult.DependencyFailed)]
    [InlineData(1075, ServiceControlResult.DependencyFailed)]
    [InlineData(1052, ServiceControlResult.CannotStop)]
    [InlineData(1061, ServiceControlResult.Busy)]
    [InlineData(1060, ServiceControlResult.NotFound)]
    [InlineData(1053, ServiceControlResult.TimedOut)]
    [InlineData(1072, ServiceControlResult.Failed)]
    public void ToControlResult_Maps_The_Win32_Error(int error, ServiceControlResult expected) =>
        Assert.Equal(expected, WindowsServicesService.ToControlResult(error));
}
#endif
