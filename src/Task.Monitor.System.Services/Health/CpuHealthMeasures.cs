namespace Task.Monitor.System.Services.Health;

public sealed record CpuHealthMeasures(
    double  BusyPercent,
    double? RunQueuePerCore) : HealthMeasures;
