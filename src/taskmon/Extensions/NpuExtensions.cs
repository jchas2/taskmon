using Task.Monitor.System.Services.Npu;

namespace Task.Monitor.Extensions;

public static class NpuExtensions
{
    public static string ToDisplayName(this NpuDevice device) =>
        device.Description is { Length: > 0 }
            ? $"NPU {device.Index}  {device.Description}"
            : $"NPU {device.Index}";

    public static string ToNpuCoresText(this NpuDevice device) => $"{device.Cores} cores";

    public static string ToNpuPercentage(this NpuDeviceMetrics device) => $"{device.ActiveRatio:000.0%}";
}
