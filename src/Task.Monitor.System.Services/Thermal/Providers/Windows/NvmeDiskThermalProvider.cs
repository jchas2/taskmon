#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Thermal.Providers.Windows;

internal sealed class NvmeDiskThermalProvider(Func<IReadOnlyList<DiskDevice>> getDisks) : IThermalProvider
{
    // The SMART / Health Information log page (0x02) is 512 bytes.
    private const int LogSize = 512;

    public void Dispose() { }

    public string Name => "NVMe SMART";

    public IEnumerable<ThermalSensor> Read()
    {
        foreach (DiskDevice disk in getDisks()) {
            if (!string.Equals(disk.BusType, "NVMe", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            byte[]? log = ReadHealthLog(disk.Index);

            if (log is null) {
                continue;
            }

            string id = disk.Index.ToString();

            if (NvmeHealthLog.CompositeCelsius(log) is { } composite) {
                yield return new ThermalSensor {
                    Component   = ThermalComponent.Disk,
                    ComponentId = id,
                    SensorName  = "Composite",
                    Celsius     = composite,
                    Source      = ThermalSource.NvmeHealthLog
                };
            }

            foreach ((int index, double celsius) in NvmeHealthLog.Sensors(log)) {
                yield return new ThermalSensor {
                    Component   = ThermalComponent.Disk,
                    ComponentId = id,
                    SensorName  = $"Sensor {index}",
                    Celsius     = celsius,
                    Source      = ThermalSource.NvmeHealthLog
                };
            }
        }
    }

    private static byte[]? ReadHealthLog(int physicalDriveIndex)
    {
        byte[] log = new byte[LogSize];

        return Nvme.TryQueryLogPage(physicalDriveIndex, WinIoCtl.NVMeLogPageHealthInfo, log)
            ? log
            : null;
    }

    public bool TryInitialise() => true;
}
#endif
