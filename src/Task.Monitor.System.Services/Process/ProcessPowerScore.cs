namespace Task.Monitor.System.Services.Process;

// Turns a process's activity into a Task-Manager-style power bucket. There is no Watt figure to be
// had - Windows' per-process energy value is uncalibrated - so this weights the CPU, GPU and disk
// activity (what the Energy Estimation Engine mostly attributes on) into one score and cuts it
// into five bands.
public static class ProcessPowerScore
{
    // Weights: GPU work costs more than the same fraction of CPU; sustained disk I/O adds a little.
    private const double CpuWeight = 100.0;
    private const double GpuWeight = 120.0;
    private const double DiskWeight = 10.0;
    private const double DiskSaturationBytesPerSecond = 5_000_000.0;

    // Band edges, in score units. Tuned so a process nudging one core reads "Low", a process
    // holding a whole core reads "High", and anything pegging the machine or the GPU reads
    // "Very High".
    private const double LowEdge = 0.5;
    private const double ModerateEdge = 3.0;
    private const double HighEdge = 12.0;
    private const double VeryHighEdge = 30.0;

    // cpuFractionOfMachine and gpuFraction are 0..1 of the whole machine; diskBytesPerSecond is raw.
    public static ProcessPowerBucket Classify(
        double cpuFractionOfMachine, double gpuFraction, double diskBytesPerSecond)
    {
        double score = Score(cpuFractionOfMachine, gpuFraction, diskBytesPerSecond);

        return score switch {
            < LowEdge => ProcessPowerBucket.VeryLow,
            < ModerateEdge => ProcessPowerBucket.Low,
            < HighEdge => ProcessPowerBucket.Moderate,
            < VeryHighEdge => ProcessPowerBucket.High,
            _ => ProcessPowerBucket.VeryHigh
        };
    }

    public static double Score(double cpuFractionOfMachine, double gpuFraction, double diskBytesPerSecond)
    {
        double cpu = Math.Max(0.0, cpuFractionOfMachine) * CpuWeight;
        double gpu = Math.Clamp(gpuFraction, 0.0, 1.0) * GpuWeight;
        double disk = Math.Clamp(diskBytesPerSecond / DiskSaturationBytesPerSecond, 0.0, 1.0) * DiskWeight;

        return cpu + gpu + disk;
    }
}
