using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services.Tests.WindowsServices;

public sealed class ServiceControlQueueTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);

    private static ServiceControlOutcome Accepted(string serviceName, bool start) =>
        new(ServiceControlResult.Succeeded);

    [Fact]
    public void An_Accepted_Request_Shows_Pending_And_Completes_Once_The_Service_Settles()
    {
        ServiceControlQueue         queue    = new();
        List<ServiceControlOutcome> outcomes = [];
        List<(string, bool)>        calls    = [];
        WindowsServiceInfo          spooler  = Service("Spooler", WindowsServiceStatus.Running);
        WindowsServicesSpecs        specs    = Specs(spooler);

        queue.Enqueue("Spooler", start: false, outcomes.Add);
        queue.Apply((name, start) => { calls.Add((name, start)); return new ServiceControlOutcome(ServiceControlResult.Succeeded); }, specs, Now);

        Assert.Equal([("Spooler", false)], calls);
        Assert.Equal(WindowsServiceStatus.StopPending, spooler.Status);
        Assert.True(queue.IsFollowing);

        queue.Settle(specs, Now.AddSeconds(2));
        Assert.Empty(outcomes);

        spooler.Status = WindowsServiceStatus.Stopped;
        queue.Settle(specs, Now.AddSeconds(4));

        Assert.Equal(ServiceControlResult.Succeeded, Assert.Single(outcomes).Result);
        Assert.False(queue.IsFollowing);
    }

    [Fact]
    public void Apply_Makes_Every_Queued_Call_In_Order()
    {
        ServiceControlQueue  queue = new();
        List<(string, bool)> calls = [];

        queue.Enqueue("Alpha", start: true, _ => { });
        queue.Enqueue("Bravo", start: false, _ => { });
        queue.Apply((name, start) => { calls.Add((name, start)); return new ServiceControlOutcome(ServiceControlResult.AccessDenied); }, Specs(), Now);

        Assert.Equal([("Alpha", true), ("Bravo", false)], calls);

        queue.Apply((name, start) => { calls.Add((name, start)); return new ServiceControlOutcome(ServiceControlResult.AccessDenied); }, Specs(), Now);

        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public void A_Failing_Callback_Does_Not_Stop_The_Worker()
    {
        ServiceControlQueue queue = new();
        queue.Enqueue("Spooler", start: false, _ => throw new InvalidOperationException("caller failed"));

        Exception? thrown = Record.Exception(() =>
            queue.Apply((_, _) => new ServiceControlOutcome(ServiceControlResult.AccessDenied, 5), Specs(), Now));

        Assert.Null(thrown);
    }

    [Fact]
    public void A_Followed_Service_That_Disappears_Is_Reported_Not_Found()
    {
        ServiceControlQueue          queue    = new();
        List<ServiceControlOutcome>  outcomes = [];

        queue.Enqueue("Spooler", start: false, outcomes.Add);
        queue.Apply(Accepted, Specs(Service("Spooler", WindowsServiceStatus.Running)), Now);
        queue.Settle(Specs(), Now);

        Assert.Equal(ServiceControlResult.NotFound, Assert.Single(outcomes).Result);
        Assert.False(queue.IsFollowing);
    }

    [Fact]
    public void A_Missing_Service_Asks_For_A_Rescan()
    {
        ServiceControlQueue         queue    = new();
        List<ServiceControlOutcome> outcomes = [];

        queue.Enqueue("Gone", start: true, outcomes.Add);

        bool rescan = queue.Apply((_, _) => new ServiceControlOutcome(ServiceControlResult.NotFound, 1060), Specs(), Now);

        Assert.True(rescan);
        Assert.Equal(ServiceControlResult.NotFound, Assert.Single(outcomes).Result);
    }

    [Fact]
    public void A_Refused_Request_Completes_At_Once_And_Is_Not_Followed()
    {
        ServiceControlQueue         queue    = new();
        List<ServiceControlOutcome> outcomes = [];
        WindowsServiceInfo          spooler  = Service("Spooler", WindowsServiceStatus.Running);

        queue.Enqueue("Spooler", start: false, outcomes.Add);

        bool rescan = queue.Apply((_, _) => new ServiceControlOutcome(ServiceControlResult.AccessDenied, 5), Specs(spooler), Now);

        ServiceControlOutcome outcome = Assert.Single(outcomes);

        Assert.False(rescan);
        Assert.Equal(ServiceControlResult.AccessDenied, outcome.Result);
        Assert.Equal(5, outcome.Win32Error);
        Assert.False(queue.IsFollowing);
        Assert.Equal(WindowsServiceStatus.Running, spooler.Status);
    }

    [Fact]
    public void A_Service_Still_Pending_After_A_Minute_Is_Reported_And_No_Longer_Followed()
    {
        ServiceControlQueue         queue    = new();
        List<ServiceControlOutcome> outcomes = [];
        WindowsServicesSpecs        specs    = Specs(Service("Slow", WindowsServiceStatus.Running));

        queue.Enqueue("Slow", start: false, outcomes.Add);
        queue.Apply(Accepted, specs, Now);

        queue.Settle(specs, Now + ServiceControlQueue.FollowTimeout - TimeSpan.FromSeconds(1));
        Assert.Empty(outcomes);

        queue.Settle(specs, Now + ServiceControlQueue.FollowTimeout);

        Assert.Equal(ServiceControlResult.StillPending, Assert.Single(outcomes).Result);
        Assert.False(queue.IsFollowing);
    }

    private static WindowsServiceInfo Service(string serviceName, WindowsServiceStatus status) =>
        new() { ServiceName = serviceName, DisplayName = serviceName, Status = status };

    [Theory]
    [InlineData(true,  WindowsServiceStatus.Running, ServiceControlResult.Succeeded)]
    [InlineData(true,  WindowsServiceStatus.Paused,  ServiceControlResult.Succeeded)]
    [InlineData(true,  WindowsServiceStatus.Stopped, ServiceControlResult.StoppedAfterStarting)]
    [InlineData(false, WindowsServiceStatus.Stopped, ServiceControlResult.Succeeded)]
    [InlineData(false, WindowsServiceStatus.Running, ServiceControlResult.DidNotStop)]
    public void SettledResult_Says_Whether_The_Service_Got_Where_It_Was_Sent(
        bool start, WindowsServiceStatus status, ServiceControlResult expected) =>
        Assert.Equal(expected, ServiceControlQueue.SettledResult(start, status));

    private static WindowsServicesSpecs Specs(params WindowsServiceInfo[] services) =>
        new() { Services = [.. services] };

    [Fact]
    public void UpdateStatuses_Sets_Each_Service_By_Name_And_Leaves_Others_Alone()
    {
        WindowsServiceInfo   spooler = Service("Spooler", WindowsServiceStatus.StopPending);
        WindowsServiceInfo   audio   = Service("AudioSrv", WindowsServiceStatus.Running);
        WindowsServicesSpecs specs   = Specs(spooler, audio);

        WindowsServicesService.UpdateStatuses(specs, [("SPOOLER", WindowsServiceStatus.Stopped)]);

        Assert.Equal(WindowsServiceStatus.Stopped, spooler.Status);
        Assert.Equal(WindowsServiceStatus.Running, audio.Status);
    }
}
