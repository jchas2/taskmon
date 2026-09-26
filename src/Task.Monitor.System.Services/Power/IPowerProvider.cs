namespace Task.Monitor.System.Services.Power;

internal interface IPowerProvider : IDisposable
{
    string Name { get; }
    bool TryInitialise();
    IEnumerable<PowerReading> Read();
}
