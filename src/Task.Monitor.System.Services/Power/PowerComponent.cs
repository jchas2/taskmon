namespace Task.Monitor.System.Services.Power;

// The hardware a power reading belongs to. Drives how a performance panel matches one back to a
// device.
public enum PowerComponent
{
    Cpu,
    Gpu,
    Disk,
    System,
    Battery
}
