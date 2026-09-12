using System.Diagnostics;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Process;

public partial class ProcessService
{
#if __WIN32__
    private const int InitialCapacity = 2048;

    private static readonly Dictionary<int, double> NoGpuPercent = new();

    private readonly Dictionary<int, ProcessSampleState> sampleStates = new(InitialCapacity);
    private readonly List<int> stalePids = new(InitialCapacity);
    private int generation;

    private void OnDoWorkProcessMetrics(ProcessMetrics metrics, ProcessSpecs specs)
    {
        // Rebuilt on its own slow cadence, and done before the enumeration rather than during it so
        // the map cannot change halfway through a cycle and label two processes inconsistently.
        WindowsServiceLookup.RefreshIfDue();

        // One enumeration per cycle, differenced against the state retained from the last one. The
        // old Processor held no state between cycles, so it enumerated every process twice per tick
        // and slept in the middle purely to obtain the same deltas.
        List<ProcessSample> samples = GetProcessSamples();
        long now = Stopwatch.GetTimestamp();

        // GpuService owns the \GPU Engine(*) provider and publishes the per pid projection of the
        // same counter array its headline figure comes from. Joining on that here, rather than
        // opening a second query, keeps the two numbers consistent and leaves one enumeration of
        // the provider per tick.
        Dictionary<int, double> gpuPercentByPid =
            GetLatest<GpuInfo>()?.Metrics.ProcessPercentTime ?? NoGpuPercent;

        generation++;

        for (int i = 0; i < samples.Count; i++) {
            ProcessSample sample = samples[i];

            metrics.ProcessCount++;
            metrics.ThreadCount += sample.ThreadCount;
            metrics.HandleCount += sample.HandleCount;

            if (!sampleStates.TryGetValue(sample.Pid, out ProcessSampleState? state)) {
                state = new ProcessSampleState();
                sampleStates.Add(sample.Pid, state);
            }

            state.Generation = generation;

            ProcessEntry entry = BuildEntry(sample);

            // Already a ratio derived by Pdh over the true interval, so there is no delta to take.
            entry.GpuTimePercent = gpuPercentByPid.GetValueOrDefault(sample.Pid);

            ApplyRates(entry, state, sample, now, specs);

            // CpuTimePercent is scaled by the Irix factor; divide it back out so the bucket is
            // always judged against the whole machine regardless of the reporting mode.
            double cpuFractionOfMachine = entry.CpuTimePercent /
                ProcessEntryCalculator.IrixFactor(specs.IrixMode, specs.LogicalProcessorCount);

            entry.PowerBucket = ProcessPowerScore.Classify(
                cpuFractionOfMachine, entry.GpuTimePercent, entry.DiskBytesPerSecond);

            // Running means the process used cpu or gpu over the interval that just elapsed.
            if (entry.CpuTimePercent > 0.0 || entry.GpuTimePercent > 0.0) {
                metrics.RunningCount++;
            }

            ApplyAverages(entry, state.Average);
            metrics.Entries.Add(entry);
        }

        PruneStaleStates();

        // After the prune, the retained state's keys are exactly the pids that were alive this
        // cycle, so the staleness check costs nothing beyond one lookup per mapped service.
        WindowsServiceLookup.RequestRefreshIfStale(sampleStates.Keys);
    }

    private static ProcessEntry BuildEntry(ProcessSample sample) =>
        new() {
            Pid = sample.Pid,
            ParentPid = sample.ParentPid,
            ThreadCount = sample.ThreadCount,
            HandleCount = sample.HandleCount,
            BasePriority = sample.BasePriority,
            IsDaemon = sample.IsDaemon,
            IsLowPriority = sample.IsLowPriority,
            IsRunningAsRoot = sample.IsRunningAsRoot,
            ProcessName = sample.ProcessName,
            FileDescription = sample.FileDescription,
            UserName = sample.UserName,
            CmdLine = sample.CmdLine,
            UsedMemory = sample.UsedMemory,
            DiskReadBytes = sample.DiskReadBytes,
            DiskWriteBytes = sample.DiskWriteBytes
        };

    private static void ApplyRates(
        ProcessEntry entry,
        ProcessSampleState state,
        ProcessSample sample,
        long now,
        ProcessSpecs specs)
    {
        // First sight of this pid. It is published now with zero rates rather than withheld for a
        // cycle: the old Processor iterated the pids of the earlier of its two samples, so a
        // process that started and exited inside one interval never appeared at all.
        if (!state.Primed) {
            Rebase(state, sample, now);
            state.Primed = true;
            return;
        }

        double elapsedSeconds = ProcessEntryCalculator.ElapsedSeconds(state.TimestampTicks, now);

        // The baseline is never moved without a real interval to divide by. Moving it while keeping
        // the old timestamp would absorb the cpu time and the bytes accumulated since the last
        // cycle into the baseline, where no later delta could ever see them.
        if (elapsedSeconds <= 0.0) {
            return;
        }

        double totalSystemTime = ProcessEntryCalculator.TotalSystemTime(
            elapsedSeconds,
            specs.LogicalProcessorCount);

        int irixFactor = ProcessEntryCalculator.IrixFactor(
            specs.IrixMode,
            specs.LogicalProcessorCount);

        entry.CpuKernelTimePercent = ProcessEntryCalculator.CpuPercent(
            sample.KernelTime - state.KernelTime,
            totalSystemTime,
            irixFactor);

        entry.CpuUserTimePercent = ProcessEntryCalculator.CpuPercent(
            sample.UserTime - state.UserTime,
            totalSystemTime,
            irixFactor);

        entry.CpuTimePercent = entry.CpuKernelTimePercent + entry.CpuUserTimePercent;

        ulong readDelta = ProcessEntryCalculator.Delta(sample.DiskReadBytes, state.DiskReadBytes);
        ulong writeDelta = ProcessEntryCalculator.Delta(sample.DiskWriteBytes, state.DiskWriteBytes);

        entry.DiskBytesPerSecond = ProcessEntryCalculator.BytesPerSecond(
            readDelta + writeDelta,
            elapsedSeconds);

        Rebase(state, sample, now);
    }

    private static void Rebase(ProcessSampleState state, ProcessSample sample, long now)
    {
        state.KernelTime = sample.KernelTime;
        state.UserTime = sample.UserTime;
        state.DiskReadBytes = sample.DiskReadBytes;
        state.DiskWriteBytes = sample.DiskWriteBytes;
        state.TimestampTicks = now;
    }

    private static void ApplyAverages(ProcessEntry entry, ProcessEntryAverage average)
    {
        average.Add(entry);

        entry.CpuTimePercentAvg = average.CpuTimePercent;
        entry.GpuTimePercentAvg = average.GpuTimePercent;
        entry.UsedMemoryAvg = average.UsedMemory;
        entry.DiskBytesPerSecondAvg = average.DiskBytesPerSecond;

        entry.CpuTimePercentMax = average.CpuTimePercentMax;
        entry.GpuTimePercentMax = average.GpuTimePercentMax;
        entry.UsedMemoryMax = average.UsedMemoryMax;
        entry.DiskBytesPerSecondMax = average.DiskBytesPerSecondMax;
    }

    private void PruneStaleStates()
    {
        // Swept every cycle rather than only when the state map outgrows the live set. A cycle in
        // which one process exits and another starts leaves those two counts equal, so the old
        // comparison skipped the sweep and the dead pid's averages survived. If Windows then
        // recycled that pid, the new process inherited the previous process's mean and max.
        if (sampleStates.Count == 0) {
            return;
        }

        stalePids.Clear();

        foreach ((int pid, ProcessSampleState state) in sampleStates) {
            if (state.Generation != generation) {
                stalePids.Add(pid);
            }
        }

        for (int i = 0; i < stalePids.Count; i++) {
            sampleStates.Remove(stalePids[i]);
        }
    }

    private void OnStopProcessMetrics()
    {
        sampleStates.Clear();
        stalePids.Clear();
        generation = 0;
    }
#endif
}
