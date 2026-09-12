using Task.Monitor.System.Services;

namespace Task.Monitor.System.Services.Tests;

public sealed class DeviceChangeNotifierTests
{
    [Fact]
    public void Start_Then_Dispose_Is_Safe()
    {
        DeviceChangeNotifier notifier = new();

        notifier.Start();
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

    [Fact]
    public void Dispose_Without_Start_Is_Safe()
    {
        DeviceChangeNotifier notifier = new();

        notifier.Dispose();
    }
}
