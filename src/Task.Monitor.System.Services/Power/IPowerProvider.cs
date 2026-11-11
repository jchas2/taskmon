namespace Task.Monitor.System.Services.Power;

internal interface IPowerProvider : IDisposable
{
    string Diagnostic => string.Empty;

    string Name { get; }

    IEnumerable<PowerReading> Read();

    bool TryInitialise();
}
