namespace Task.Monitor.System.Services.Thermal;

internal interface IThermalProvider : IDisposable
{
    string Name { get; }

    IEnumerable<ThermalSensor> Read();

    bool TryInitialise();
}
