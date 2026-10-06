#if __APPLE__
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Cpu;

public partial class CpuService
{
    private uint[] previousCoreTicks = Array.Empty<uint>();
    private int coreCount;
    private bool cpuPrimed;

    private long aggUserDelta;
    private long aggSystemDelta;
    private long aggIdleDelta;
    private long aggNiceDelta;

    private void OnStartCpuCore() => ResetCpuState();

    private void OnStopCpuCore() => ResetCpuState();

    private void ResetCpuState()
    {
        previousCoreTicks = Array.Empty<uint>();
        coreCount = 0;
        cpuPrimed = false;
        aggUserDelta = aggSystemDelta = aggIdleDelta = aggNiceDelta = 0;
    }

    private void OnDoWorkCpuCore(CpuInfo cpuInfo)
    {
        uint[]? current = CaptureCpuTicks(out int cpus);

        if (current == null || cpus <= 0) {
            return;
        }

        if (!cpuPrimed || cpus != coreCount) {
            previousCoreTicks = current;
            coreCount = cpus;
            cpuPrimed = true;
            return;
        }

        const int states = MachHost.CPU_STATE_MAX;

        long sumUser = 0;
        long sumSystem = 0;
        long sumIdle = 0;
        long sumNice = 0;

        ImmutableArray<CpuInfo.CpuCoreMetric>.Builder builder =
            ImmutableArray.CreateBuilder<CpuInfo.CpuCoreMetric>(cpus);

        for (int i = 0; i < cpus; i++) {
            int baseIndex = i * states;

            long userDelta   = TickDelta(current[baseIndex + MachHost.CPU_STATE_USER],   previousCoreTicks[baseIndex + MachHost.CPU_STATE_USER]);
            long systemDelta = TickDelta(current[baseIndex + MachHost.CPU_STATE_SYSTEM], previousCoreTicks[baseIndex + MachHost.CPU_STATE_SYSTEM]);
            long idleDelta   = TickDelta(current[baseIndex + MachHost.CPU_STATE_IDLE],   previousCoreTicks[baseIndex + MachHost.CPU_STATE_IDLE]);
            long niceDelta   = TickDelta(current[baseIndex + MachHost.CPU_STATE_NICE],   previousCoreTicks[baseIndex + MachHost.CPU_STATE_NICE]);

            long busy  = userDelta + systemDelta + niceDelta;
            long total = busy + idleDelta;

            double value = total > 0 ? Math.Clamp((double)busy / total, 0.0, 1.0) : 0.0;
            builder.Add(new CpuInfo.CpuCoreMetric(i.ToString(), value));

            sumUser += userDelta;
            sumSystem += systemDelta;
            sumIdle += idleDelta;
            sumNice += niceDelta;
        }

        builder.Sort(static (left, right) => CpuCoreMetricOrdering.Compare(left.Name, right.Name));
        cpuInfo.CoreMetrics = builder.ToImmutable();

        aggUserDelta = sumUser;
        aggSystemDelta = sumSystem;
        aggIdleDelta = sumIdle;
        aggNiceDelta = sumNice;

        previousCoreTicks = current;
        coreCount = cpus;
    }

    private void OnDoWorkCpuMetrics(CpuInfo cpuInfo)
    {
        long total = aggUserDelta + aggSystemDelta + aggIdleDelta + aggNiceDelta;

        if (total <= 0) {
            return;
        }

        cpuInfo.Metrics.CpuPercentUserTime   = (double)(aggUserDelta + aggNiceDelta) / total;
        cpuInfo.Metrics.CpuPercentKernelTime = (double)aggSystemDelta / total;
        cpuInfo.Metrics.CpuPercentIdleTime   = (double)aggIdleDelta / total;
        cpuInfo.Metrics.CpuTotalTime         =
            cpuInfo.Metrics.CpuPercentKernelTime + cpuInfo.Metrics.CpuPercentUserTime;
    }

    private static long TickDelta(uint current, uint previous) =>
        current >= previous ? current - previous : 0;

    private static uint[]? CaptureCpuTicks(out int cpuCount)
    {
        cpuCount = 0;

        int result = MachHost.host_processor_info(
            MachHost.mach_host_self(),
            MachHost.PROCESSOR_CPU_LOAD_INFO,
            out uint numCpus,
            out IntPtr cpuInfo,
            out uint numCpuInfo);

        if (result != 0 || cpuInfo == IntPtr.Zero || numCpus == 0) {
            return null;
        }

        try {
            uint[] ticks = new uint[numCpuInfo];

            for (int i = 0; i < numCpuInfo; i++) {
                ticks[i] = unchecked((uint)Marshal.ReadInt32(cpuInfo, i * sizeof(int)));
            }

            cpuCount = (int)numCpus;
            return ticks;
        }
        finally {
            IntPtr size = new((int)numCpuInfo * sizeof(int));
            MachHost.vm_deallocate(MachHost.mach_task_self(), cpuInfo, size);
        }
    }
}
#endif
