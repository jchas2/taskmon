namespace Task.Monitor.System.Services.Process;

// Turns a process's activity into a Task-Manager-style power score. 
public static class ProcessPowerScore
{
    private const double CpuWeight = 100.0;
    private const double GpuWeight = 120.0;
    private const double DiskWeight = 10.0;
    private const double DiskSaturationBytesPerSecond = 5_000_000.0;

    private const double LowEdge = 0.5;
    private const double ModerateEdge = 3.0;
    private const double HighEdge = 12.0;
    private const double VeryHighEdge = 30.0;

    public static ProcessPowerBucket Classify(
        double cpuFractionOfMachine, 
        double gpuFraction, 
        double diskBytesPerSecond)
    {
        double score = Score(cpuFractionOfMachine, gpuFraction, diskBytesPerSecond);

        return score switch {
            < LowEdge      => ProcessPowerBucket.VeryLow,
            < ModerateEdge => ProcessPowerBucket.Low,
            < HighEdge     => ProcessPowerBucket.Moderate,
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
