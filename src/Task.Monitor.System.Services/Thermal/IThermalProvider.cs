namespace Task.Monitor.System.Services.Thermal;

internal interface IThermalProvider : IDisposable
{
    string Diagnostic => string.Empty;

    string Name { get; }

    IEnumerable<ThermalSensor> Read();

    bool TryInitialise();
}
