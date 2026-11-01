namespace Task.Monitor.System.Services.Health;

public sealed record HealthReport(
    DateTime         TimeUtc, 
    HealthReportKind Kind, 
    string           Title, 
    string           Detail, 
    string           Source);
