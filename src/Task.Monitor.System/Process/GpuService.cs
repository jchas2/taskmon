using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Process;

public static partial class GpuService
{
    public static Dictionary<int, long> GetProcessStats() => GetProcessStatsInternal();
}
