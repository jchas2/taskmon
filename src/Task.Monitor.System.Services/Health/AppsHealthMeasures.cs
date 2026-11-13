namespace Task.Monitor.System.Services.Health;

public sealed record AppsHealthMeasures(
    int? NotResponding,
    int  Faults) : HealthMeasures;
