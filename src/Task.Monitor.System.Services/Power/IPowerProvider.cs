namespace Task.Monitor.System.Services.Power;

// One source of power readings - a vendor GPU SDK, the ACPI power meter, the battery. Probed once
// at start, kept if it initialises, read each cycle; dropped and re-probed on a refresh.
internal interface IPowerProvider : IDisposable
{
    string Name { get; }

    bool TryInitialise();

    IEnumerable<PowerReading> Read();
}
