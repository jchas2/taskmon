using System.Diagnostics;

namespace Task.Monitor.System.Services.Process;

public partial class ProcessService
{
    private const int InitialCapacity = 2048;

    // Shared empty map used when no per-process GPU data is available for a cycle.
    private static readonly Dictionary<int, double> NoGpuPercent = new();

    private readonly Dictionary<int, ProcessSampleState> sampleStates = new(InitialCapacity);
    private readonly List<int> stalePids = new(InitialCapacity);
    private int generation;

    // Platform-neutral metrics core: turns a per-platform list of ProcessSample into ProcessEntry
    // rows, applying rates, averages and power classification. The only platform-specific input is
    // how the samples (and the optional GPU-percent map) are gathered.
    private void BuildMetrics(
        ProcessMetrics metrics,
        ProcessSpecs specs,
        List<ProcessSample> samples,
        Dictionary<int, double> gpuPercentByPid)
    {
        long now = Stopwatch.GetTimestamp();

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

            entry.GpuTimePercent = gpuPercentByPid.GetValueOrDefault(sample.Pid);

            ApplyRates(
                entry,
                state,
                sample,
                now,
                specs);

            // CpuTimePercent is scaled by the Irix factor; divide it out so the bucket is
            // always calculated against the whole machine regardless of the reporting mode.
            double cpuFractionOfMachine = entry.CpuTimePercent /
                ProcessEntryCalculator.IrixFactor(specs.IrixMode, specs.LogicalProcessorCount);

            entry.PowerBucket = ProcessPowerScore.Classify(
                cpuFractionOfMachine,
                entry.GpuTimePercent,
                entry.DiskBytesPerSecond);

            if (entry.CpuTimePercent > 0.0 || entry.GpuTimePercent > 0.0) {
                metrics.RunningCount++;
            }

            ApplyAverages(entry, state.Average);
            metrics.Entries.Add(entry);
        }

        PruneStaleStates();
    }

    private static ProcessEntry BuildEntry(ProcessSample sample) =>
        new() {
            Pid             = sample.Pid,
            ParentPid       = sample.ParentPid,
            ThreadCount     = sample.ThreadCount,
            HandleCount     = sample.HandleCount,
            BasePriority    = sample.BasePriority,
            IsDaemon        = sample.IsDaemon,
            IsLowPriority   = sample.IsLowPriority,
            IsRunningAsRoot = sample.IsRunningAsRoot,
            ProcessName     = sample.ProcessName,
            FileDescription = sample.FileDescription,
            UserName        = sample.UserName,
            CmdLine         = sample.CmdLine,
            UsedMemory      = sample.UsedMemory,
            DiskReadBytes   = sample.DiskReadBytes,
            DiskWriteBytes  = sample.DiskWriteBytes
        };

    private static void ApplyRates(
        ProcessEntry entry,
        ProcessSampleState state,
        ProcessSample sample,
        long now,
        ProcessSpecs specs)
    {
        if (!state.Primed) {
            Rebase(state, sample, now);
            state.Primed = true;
            return;
        }

        double elapsedSeconds = ProcessEntryCalculator.ElapsedSeconds(state.TimestampTicks, now);

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

        ulong readDelta  = ProcessEntryCalculator.Delta(sample.DiskReadBytes, state.DiskReadBytes);
        ulong writeDelta = ProcessEntryCalculator.Delta(sample.DiskWriteBytes, state.DiskWriteBytes);

        entry.DiskBytesPerSecond = ProcessEntryCalculator.BytesPerSecond(
            readDelta + writeDelta,
            elapsedSeconds);

        Rebase(state, sample, now);
    }

    private static void Rebase(ProcessSampleState state, ProcessSample sample, long now)
    {
        state.KernelTime     = sample.KernelTime;
        state.UserTime       = sample.UserTime;
        state.DiskReadBytes  = sample.DiskReadBytes;
        state.DiskWriteBytes = sample.DiskWriteBytes;
        state.TimestampTicks = now;
    }

    private static void ApplyAverages(ProcessEntry entry, ProcessEntryAverage average)
    {
        average.Add(entry);

        entry.CpuTimePercentAvg     = average.CpuTimePercent;
        entry.GpuTimePercentAvg     = average.GpuTimePercent;
        entry.UsedMemoryAvg         = average.UsedMemory;
        entry.DiskBytesPerSecondAvg = average.DiskBytesPerSecond;

        entry.CpuTimePercentMax     = average.CpuTimePercentMax;
        entry.GpuTimePercentMax     = average.GpuTimePercentMax;
        entry.UsedMemoryMax         = average.UsedMemoryMax;
        entry.DiskBytesPerSecondMax = average.DiskBytesPerSecondMax;
    }

    private void PruneStaleStates()
    {
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
}
