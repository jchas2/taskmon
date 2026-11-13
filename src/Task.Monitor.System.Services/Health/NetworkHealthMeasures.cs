namespace Task.Monitor.System.Services.Health;

public sealed record NetworkHealthMeasures(
    double ReceiveBytesPerSecond,
    double SendBytesPerSecond,
    double DiscardsPerSecond,
    double ErrorsPerSecond) : HealthMeasures;
