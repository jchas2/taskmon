namespace Task.Monitor.System.Services.Thermal;

internal interface IThermalProvider : IDisposable
{
    string Name { get; }
    bool TryInitialise();
    IEnumerable<ThermalSensor> Read();
}
