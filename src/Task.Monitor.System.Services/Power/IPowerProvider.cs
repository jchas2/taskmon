namespace Task.Monitor.System.Services.Power;

internal interface IPowerProvider : IDisposable
{
    string Name { get; }

    IEnumerable<PowerReading> Read();

    bool TryInitialise();
}
