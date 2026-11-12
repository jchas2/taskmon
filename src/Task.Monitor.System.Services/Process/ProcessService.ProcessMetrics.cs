using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Process;

public partial class ProcessService
{
    private const int InitialCapacity = 2048;

    private static readonly Dictionary<int, double> NoGpuPercent = new();

    private readonly Dictionary<int, ProcessSampleState> sampleStates = new(InitialCapacity);
    private readonly List<int>                           stalePids    = new(InitialCapacity);
    
    private int      generation;
    private DateTime previousSampleUtc;
    private long     previousSampleTicks;

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

    private void ApplyRates(
        ProcessEntry       entry,
        ProcessSampleState state,
        ProcessSample      sample,
        long               now,
        ProcessSpecs       specs)
    {
        if (!state.Primed) {
            entry.DiskBytesPerSecond = FirstDiskBytesPerSecond(
                sample,
                previousSampleUtc,
                ProcessEntryCalculator.ElapsedSeconds(previousSampleTicks, now));

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

        ulong readDelta  = CounterRate.Delta(sample.DiskReadBytes, state.DiskReadBytes);
        ulong writeDelta = CounterRate.Delta(sample.DiskWriteBytes, state.DiskWriteBytes);

        entry.DiskBytesPerSecond = CounterRate.PerSecond(
            readDelta + writeDelta,
            elapsedSeconds);

        Rebase(state, sample, now);
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
            CommittedMemory = sample.CommittedMemory,
            DiskReadBytes   = sample.DiskReadBytes,
            DiskWriteBytes  = sample.DiskWriteBytes
        };

    private void BuildMetrics(
        ProcessMetrics          metrics,
        ProcessSpecs            specs,
        List<ProcessSample>     samples,
        Dictionary<int, double> gpuPercentByPid)
    {
        long     now    = Stopwatch.GetTimestamp();
        DateTime utcNow = DateTime.UtcNow;

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

        previousSampleTicks = now;
        previousSampleUtc   = utcNow;

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

    internal static string DescribeFailures(IReadOnlyDictionary<int, int> failuresByError) =>
        string.Join(", ", failuresByError
            .OrderByDescending(failure => failure.Value)
            .ThenBy(failure => failure.Key)
            .Select(failure => $"{ErrorCodes.Describe(failure.Key)} x{failure.Value}"));

    internal static double FirstDiskBytesPerSecond(
        ProcessSample sample,
        DateTime      previousSampleUtc,
        double        elapsedSeconds) =>
        previousSampleUtc != default && sample.StartTimeUtc >= previousSampleUtc
            ? CounterRate.PerSecond(sample.DiskReadBytes + sample.DiskWriteBytes, elapsedSeconds)
            : 0.0;

    private void OnStopProcessMetrics()
    {
        sampleStates.Clear();
        stalePids.Clear();
        generation        = 0;
        previousSampleUtc = default;
    }
    
    private static void Rebase(ProcessSampleState state, ProcessSample sample, long now)
    {
        state.KernelTime     = sample.KernelTime;
        state.UserTime       = sample.UserTime;
        state.DiskReadBytes  = sample.DiskReadBytes;
        state.DiskWriteBytes = sample.DiskWriteBytes;
        state.TimestampTicks = now;
    }
}
