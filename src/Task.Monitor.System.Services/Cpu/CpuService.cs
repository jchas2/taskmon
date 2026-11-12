using System.Diagnostics;

namespace Task.Monitor.System.Services.Cpu;

public sealed partial class CpuService : WorkerService
{
    private const double BytesPerMegabyte = 1024.0 * 1024.0;

    private CpuSpecs cpuSpecs = new();

    public static string GetCpuName() => GetCpuNameInternal();

    private void LogSpecs(CpuSpecs specs)
    {
        string name = string.IsNullOrEmpty(specs.CpuName) ? "name NOT FOUND" : specs.CpuName;

        Trace.WriteLine(
            $"{ServiceName}: {name}, {specs.CpuCores} logical processors, {specs.CpuSockets} socket(s), " +
            $"{specs.CpuFrequency:0} MHz, L1 {Megabytes(specs.CpuL1CacheBytes)}, " +
            $"L2 {Megabytes(specs.CpuL2CacheBytes)}, L3 {Megabytes(specs.CpuL3CacheBytes)}");

        if (specs.CpuPerformanceCores + specs.CpuEfficiencyCores + specs.CpuSuperCores > 0) {
            Trace.WriteLine(
                $"{ServiceName}: {specs.CpuSuperCores} super cores at {specs.CpuSuperFrequency:0} MHz, " +
                $"{specs.CpuPerformanceCores} performance cores at {specs.CpuPerformanceFrequency:0} MHz, " +
                $"{specs.CpuEfficiencyCores} efficiency cores at {specs.CpuEfficiencyFrequency:0} MHz");
        }
#if __APPLE__
        else {
            Trace.WriteLine($"{ServiceName}: performance levels NOT FOUND, the hw.perflevel sysctls gave no cores");
        }
#endif

#if __WIN32__
        Trace.WriteLine(
            $"{ServiceName}: virtualisation firmware {(specs.CpuVirtualizationFirmwareEnabled ? "enabled" : "disabled")}");
#endif
    }

    private static string Megabytes(ulong bytes) => $"{bytes / BytesPerMegabyte:0.#} MB";

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        CpuInfo cpuInfo = new();
        cpuInfo.Specs = cpuSpecs;

        OnDoWorkCpuCore(cpuInfo);
        OnDoWorkCpuMetrics(cpuInfo);
        OnDoWorkPressureMetrics(cpuInfo);
        Publish(cpuInfo);
    }

    protected override void OnStart()
    {
        OnStartCpuSpecs(ref cpuSpecs);
        LogSpecs(cpuSpecs);
        OnStartCpuCore();
    }

    protected override void OnStop()
    {
        OnStopCpuCore();
    }
}
