namespace Task.Monitor.System.Services.Health;

public sealed record HealthCause(
    HealthCauseKind Kind,
    double          Value,
    string          Subject = "",
    double          Limit   = 0.0);
