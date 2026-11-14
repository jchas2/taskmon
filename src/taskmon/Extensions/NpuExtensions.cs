using System.Globalization;
using Task.Monitor.Resources;
using Task.Monitor.System.Services.Npu;

namespace Task.Monitor.Extensions;

public static class NpuExtensions
{
    public static string ToDisplayName(this NpuDevice device) =>
        device.Description is { Length: > 0 }
            ? string.Format(CultureInfo.CurrentCulture, Strings.NpuDisplayName, device.Index, device.Description)
            : string.Format(CultureInfo.CurrentCulture, Strings.NpuDisplayNameShort, device.Index);

    public static string ToNpuCoresText(this NpuDevice device) =>
        string.Format(CultureInfo.CurrentCulture, Strings.NpuCores, device.Cores);

    public static string ToNpuPercentage(this NpuDeviceMetrics device) => $"{device.ActiveRatio:000.0%}";
}
