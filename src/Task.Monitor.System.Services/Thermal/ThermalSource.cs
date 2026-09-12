namespace Task.Monitor.System.Services.Thermal;

// Where a reading came from. Useful for diagnostics and for deciding which of several sensors on
// one component is the headline (a vendor SDK beats an ACPI zone).
public enum ThermalSource
{
    NvApi,
    Adl,
    Igcl,
    NvmeHealthLog,
    AtaSmart,
    AcpiThermalZone,
    Battery,
    KernelMsr
}
