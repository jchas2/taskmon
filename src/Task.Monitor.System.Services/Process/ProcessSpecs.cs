namespace Task.Monitor.System.Services.Process;

public sealed class ProcessSpecs
{
    // Deliberately thin. Unlike the other services there is no hardware to describe here, but the
    // Specs/Metrics shape is kept so every published Info type reads the same way. What it does
    // carry is the two static inputs the per process percentage maths depends on.
    public int LogicalProcessorCount { get; set; } = Environment.ProcessorCount;

    // Irix mode reports 100% as full utilisation of a SINGLE core, matching macOS Activity Monitor.
    // Non Irix reports 100% as full utilisation of ALL cores, matching Windows Task Manager.
    public bool IrixMode { get; set; }
}
