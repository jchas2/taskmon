namespace Task.Monitor.System.Services.Power;

public sealed class PowerInfo
{
    public PowerMetrics Metrics { get; set; } = new();
}
