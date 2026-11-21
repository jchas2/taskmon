#if __WIN32__
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Native;
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Power.Providers.Windows;

// The peak power of an NVMe drive's top power state.
internal sealed class NvmeRatedPowerProvider(Func<IReadOnlyList<DiskDevice>> getDisks) : IReadingProvider<PowerReading>
{
    private readonly Dictionary<int, double?> cache = new();

    public string Name => "NVMe rated power";

    public IEnumerable<PowerReading> Read()
    {
        foreach (DiskDevice disk in getDisks()) {
            if (!string.Equals(disk.BusType, "NVMe", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            if (!cache.TryGetValue(disk.Index, out double? watts)) {
                watts             = ReadPeakWatts(disk.Index);
                cache[disk.Index] = watts;
            }

            if (watts is { } value) {
                yield return new PowerReading {
                    Component   = PowerComponent.Disk,
                    ComponentId = disk.Index.ToString(),
                    Rail        = PowerRails.Drive,
                    Watts       = value,
                    IsRated     = true,
                    Source      = PowerSource.NvmeRated
                };
            }
        }
    }

    private static double? ReadPeakWatts(int physicalDriveIndex)
    {
        byte[] identify = new byte[WinIoCtl.NVMeIdentifyControllerSize];

        return NvmeDrive.TryQueryIdentify(physicalDriveIndex, WinIoCtl.NVMeIdentifyCnsController, identify)
            ? NvmePowerState.PeakWatts(identify)
            : null;
    }
}
#endif
