using System.Collections.Concurrent;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Services.WindowsServices;

internal sealed class ServiceControlQueue
{
    internal static readonly TimeSpan FollowTimeout = TimeSpan.FromSeconds(60);

    private readonly ConcurrentQueue<Request>      requests = new();
    private readonly Dictionary<string, Following> followed = new(StringComparer.OrdinalIgnoreCase);

    private sealed record Following(bool Start, DateTime SinceUtc, Action<ServiceControlOutcome> Completed);

    private sealed record Request(string ServiceName, bool Start, Action<ServiceControlOutcome> Completed);

    public bool Apply(Func<string, bool, ServiceControlOutcome> control, WindowsServicesSpecs specs, DateTime nowUtc)
    {
        bool rescan = false;

        while (requests.TryDequeue(out Request? request)) {
            ServiceControlOutcome outcome = control(request.ServiceName, request.Start);

            if (outcome.Result != ServiceControlResult.Succeeded) {
                rescan |= outcome.Result == ServiceControlResult.NotFound;
                Complete(request.Completed, outcome);
                continue;
            }

            if (Find(specs, request.ServiceName) is { } service) {
                service.Status = request.Start
                    ? WindowsServiceStatus.StartPending
                    : WindowsServiceStatus.StopPending;
            }

            followed[request.ServiceName] = new Following(request.Start, nowUtc, request.Completed);
        }

        return rescan;
    }

    private static void Complete(Action<ServiceControlOutcome> completed, ServiceControlOutcome outcome)
    {
        try {
            completed(outcome);
        }
        catch (Exception ex) {
            // The caller's failure must not stop the worker.
            ExceptionHelper.LogException(ex);
        }
    }

    public void Enqueue(string serviceName, bool start, Action<ServiceControlOutcome> completed) =>
        requests.Enqueue(new Request(serviceName, start, completed));

    private static WindowsServiceInfo? Find(WindowsServicesSpecs specs, string serviceName) =>
        specs.Services.Find(service => string.Equals(service.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));

    public bool IsFollowing => followed.Count > 0;

    public void Settle(WindowsServicesSpecs specs, DateTime nowUtc)
    {
        foreach ((string serviceName, Following following) in followed.ToList()) {
            WindowsServiceInfo? service = Find(specs, serviceName);

            ServiceControlResult? result =
                service is null                              ? ServiceControlResult.NotFound                    :
                !service.IsPending                           ? SettledResult(following.Start, service.Status) :
                nowUtc - following.SinceUtc >= FollowTimeout ? ServiceControlResult.StillPending                :
                                                               null;

            if (result is not { } settled) {
                continue;
            }

            followed.Remove(serviceName);
            Complete(following.Completed, new ServiceControlOutcome(settled));
        }
    }

    internal static ServiceControlResult SettledResult(bool start, WindowsServiceStatus status) => (start, status) switch {
        (true,  WindowsServiceStatus.Stopped)     => ServiceControlResult.StoppedAfterStarting,
        (false, not WindowsServiceStatus.Stopped) => ServiceControlResult.DidNotStop,
                                                _ => ServiceControlResult.Succeeded
    };
}
