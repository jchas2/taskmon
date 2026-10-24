namespace Task.Monitor.System.Services.Npu;

public sealed class NpuMetrics
{
    public List<NpuDeviceMetrics> Devices { get; set; } = new();
}
