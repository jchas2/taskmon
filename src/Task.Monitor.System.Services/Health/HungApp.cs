namespace Task.Monitor.System.Services.Health;

public sealed record HungApp(int Pid, string Name, double Seconds);
