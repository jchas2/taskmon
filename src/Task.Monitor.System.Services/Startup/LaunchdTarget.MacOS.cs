#if __APPLE__
using Task.Monitor.System.Services.LaunchdServices;

namespace Task.Monitor.System.Services.Startup;

internal readonly record struct LaunchdTarget(LaunchdDomain Domain, uint Uid, string Label, bool BlockedByBackgroundItems)
{
    public string ServiceTarget =>
        Domain == LaunchdDomain.System
            ? $"system/{Label}"
            : $"user/{Uid}/{Label}";
}
#endif
