#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Memory;

public partial class MemoryService
{
    private const int BytesPerMegabyte = 1024 * 1024;

    private void OnStartMemorySpecs(MemorySpecs specs)
    {
        // Apple Silicon has no SMBIOS Type 17 / DIMM slots (memory is unified and on-package), so a
        // single synthetic device describes the whole pool. Per-module fields are unavailable.
        specs.Devices.Clear();

        if (!Sys.SysctlByNameLong("hw.memsize", out long memSize) || memSize <= 0) {
            return;
        }

        specs.Devices.Add(new MemoryDevice {
            SizeInMegabytes      = (uint)(memSize / BytesPerMegabyte),
            Speed                = 0,
            ConfiguredClockSpeed = 0,
            Slot                 = 0,
            MemoryType           = "LPDDR",
            FormFactor           = "Unified",
            DeviceLocator        = "Unified Memory",
            BankLocator          = string.Empty,
            Manufacturer         = "Apple",
            SerialNumber         = string.Empty,
            PartNumber           = string.Empty
        });
    }
}
#endif
