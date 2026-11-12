using Task.Monitor.System.Services;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.System.Services.Tests;

public sealed partial class DeviceChangeNotifierTests
{
    [Fact]
    public void Dispose_Without_Start_Is_Safe()
    {
        DeviceChangeNotifier notifier = new();

        notifier.Dispose();
    }

    [Fact]
    public void Start_And_Dispose_Are_Idempotent()
    {
        DeviceChangeNotifier notifier = new();

        notifier.Start();
        notifier.Start();
        notifier.Dispose();
        notifier.Dispose();
    }

    // Other tests start notifiers too, so this only checks that registering wrote a result and that
    // none of it failed.
    [Fact]
    public void Start_Logs_The_Registrations()
    {
        using TraceCapture capture = new();

        DeviceChangeNotifier notifier = new();

        try {
            notifier.Start();
        }
        finally {
            notifier.Dispose();
        }

        IReadOnlyList<string> lines = capture.LinesContaining($"{nameof(DeviceChangeNotifier)}: ");

        Assert.Contains(lines, line => line.Contains(" OK for ", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("FAILED", StringComparison.Ordinal));
    }

    [Fact]
    public void Start_Then_Dispose_Is_Safe()
    {
        DeviceChangeNotifier notifier = new();

        notifier.Start();
        notifier.Dispose();
    }
}
