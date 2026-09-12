namespace Task.Monitor.System.Services.Thermal;

// One source of temperature readings - a vendor GPU SDK, an NVMe log page, the ACPI thermal zones.
// The service probes every provider once at start, keeps the ones that initialise, and reads them
// each cycle. A provider that throws mid-run is dropped and re-probed on the next refresh.
internal interface IThermalProvider : IDisposable
{
    string Name { get; }

    bool TryInitialise();

    IEnumerable<ThermalSensor> Read();
}
