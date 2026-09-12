namespace Task.Monitor.System.Services.Thermal;

// The kind of hardware a temperature reading belongs to. Drives how the thermals screen groups
// sensors and how the performance panels match one back to a device.
public enum ThermalComponent
{
    Cpu,
    Gpu,
    Disk,
    Memory,
    Battery,
    Chipset,
    Ambient,
    Other
}
