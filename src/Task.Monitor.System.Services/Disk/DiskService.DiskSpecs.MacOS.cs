#if __APPLE__
using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Disk;

public partial class DiskService
{
    private static DiskDevice? BuildDiskDevice(uint entry)
    {
        int index = ParseDiskIndex(ReadBsdName(entry));

        if (index < 0) {
            return null;
        }

        string? interconnect = ReadPhysicalInterconnect(entry);

        if (IsVirtualInterconnect(interconnect)) {
            return null;
        }

        DiskDevice device = new();
        device.Index = index;

        if (IOKit.SearchProperty(entry, "Size", IOKit.IterateRecursively) is long size) {
            device.Capacity = size;
        }

        ReadDeviceCharacteristics(entry, device);
        device.BusType = interconnect ?? device.BusType;

        return device;
    }

    private static void EnumerateDiskDevices(DiskSpecs specs)
    {
        int result = IOKit.IOServiceGetMatchingServices(0, IOKit.IOServiceMatching(BlockStorageDriverClass), out nint iteratorRef);
        using IOObjectScope iterator = new(iteratorRef);

        if (result != 0 || iterator.IsNull) {
            Trace.WriteLine(result != 0
                ? $"{nameof(DiskService)}: IOKit {BlockStorageDriverClass} FAILED {ErrorCodes.DescribeKernReturn(result)}"
                : $"{nameof(DiskService)}: IOKit {BlockStorageDriverClass} NOT FOUND");

            return;
        }

        int  matched = 0;
        uint entryRef;

        while ((entryRef = IOKit.IOIteratorNext(iterator)) != 0) {
            using IOObjectScope entry = new(entryRef);
            DiskDevice? device = BuildDiskDevice(entry);

            matched++;

            if (device != null) {
                specs.Devices.Add(device);
            }
        }

        specs.Devices.Sort((left, right) => left.Index.CompareTo(right.Index));

        Trace.WriteLine(
            $"{nameof(DiskService)}: IOKit {BlockStorageDriverClass} OK, {matched} matched, {specs.Devices.Count} physical");
    }

    private static void EnumerateVolumes(DiskSpecs specs)
    {
        // TODO: Interop replace. 
        foreach (DriveInfo drive in DriveInfo.GetDrives()) {
            try {
                if (!drive.IsReady || drive.TotalSize <= 0) {
                    continue;
                }

                DiskVolume volume = new();
                volume.VolumeName         = drive.Name;
                volume.MountPoints        = [drive.Name];
                volume.Label              = string.IsNullOrEmpty(drive.VolumeLabel) ? drive.Name : drive.VolumeLabel;
                volume.FileSystem         = drive.DriveFormat;
                volume.DriveType          = drive.DriveType.ToString();
                volume.IsReady            = true;
                volume.FormattedCapacity  = drive.TotalSize;
                volume.AvailableFreeSpace = drive.AvailableFreeSpace;
                volume.UsedRatio          = drive.TotalSize > 0
                    ? 1.0 - drive.AvailableFreeSpace / (double)drive.TotalSize
                    : 0.0;

                specs.UnattachedVolumes.Add(volume);
            }
            catch {
                // Some special mounts throw on property access; skip them.
            }
        }
    }

    private void OnStartDiskSpecs(DiskSpecs specs)
    {
        EnumerateDiskDevices(specs);
        EnumerateVolumes(specs);
    }

    private static void ReadDeviceCharacteristics(uint entry, DiskDevice device)
    {
        if (ReadCharacteristics(entry, "Device Characteristics") is not { } dict) {
            return;
        }

        device.Model            = TrimmedString(dict, "Product Name") ?? device.Model;
        device.Manufacturer     = TrimmedString(dict, "Vendor Name") ?? device.Manufacturer;
        device.FirmwareRevision = TrimmedString(dict, "Product Revision Level") ?? device.FirmwareRevision;
        device.SerialNumber     = TrimmedString(dict, "Serial Number") ?? device.SerialNumber;

        device.MediaType = dict.GetValueOrDefault("Medium Type") switch {
            "Solid State"                     => "SSD",
            "Rotational"                      => "HDD",
            string { Length: > 0 } mediumType => mediumType,
                                            _ => device.MediaType
        };
    }
}
#endif
