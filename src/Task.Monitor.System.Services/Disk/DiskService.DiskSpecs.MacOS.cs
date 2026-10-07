#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Disk;

public partial class DiskService
{
    private void OnStartDiskSpecs(DiskSpecs specs)
    {
        EnumerateDiskDevices(specs);
        EnumerateVolumes(specs);
    }

    private static void EnumerateDiskDevices(DiskSpecs specs)
    {
        if (IOKit.IOServiceGetMatchingServices(0, IOKit.IOServiceMatching(BlockStorageDriverClass), out nint iterator) != 0 ||
            iterator == IntPtr.Zero) {
            return;
        }

        uint entry;

        while ((entry = IOKit.IOIteratorNext(iterator)) != 0) {
            try {
                DiskDevice? device = BuildDiskDevice(entry);

                if (device != null) {
                    specs.Devices.Add(device);
                }
            }
            finally {
                IOKit.IOObjectRelease(entry);
            }
        }

        IOKit.IOObjectRelease(iterator);
        specs.Devices.Sort((left, right) => left.Index.CompareTo(right.Index));
    }

    private static DiskDevice? BuildDiskDevice(uint entry)
    {
        string? bsdName = SearchStringProperty(entry, "BSD Name", IterateRecursively);
        int index = ParseDiskIndex(bsdName);

        if (index < 0) {
            return null;
        }

        DiskDevice device = new();
        device.Index = index;

        if (SearchNumberProperty(entry, "Size", IterateRecursively, out long size)) {
            device.Capacity = size;
        }

        ReadDeviceCharacteristics(entry, device);
        ReadProtocolCharacteristics(entry, device);

        return device;
    }

    private static void ReadDeviceCharacteristics(uint entry, DiskDevice device)
    {
        using CFScope cfKey = new(CoreFoundation.CFStringCreate("Device Characteristics"));
        
        using CFScope dictRef = new(IOKit.IORegistryEntrySearchCFProperty(
            entry, 
            IOServicePlane, 
            cfKey, 
            nint.Zero, 
            IterateRecursively | IterateParents));

        if (dictRef.IsNull) {
            return;
        }

        Dictionary<string, nint> dict = CoreFoundation.ToDictionary(dictRef);

        device.Model            = ReadTrimmedString(dict, "Product Name") ?? device.Model;
        device.Manufacturer     = ReadTrimmedString(dict, "Vendor Name") ?? device.Manufacturer;
        device.FirmwareRevision = ReadTrimmedString(dict, "Product Revision Level") ?? device.FirmwareRevision;
        device.SerialNumber     = ReadTrimmedString(dict, "Serial Number") ?? device.SerialNumber;

        if (dict.TryGetValue("Medium Type", out nint medium)) {
            string? mediumType = CoreFoundation.GetString(medium);

            device.MediaType = mediumType switch {
                "Solid State" => "SSD",
                "Rotational"  => "HDD",
                _             => string.IsNullOrEmpty(mediumType) ? device.MediaType : mediumType
            };
        }
    }

    private static void ReadProtocolCharacteristics(uint entry, DiskDevice device)
    {
        using CFScope cfKey = new(CoreFoundation.CFStringCreate("Protocol Characteristics"));
        
        using CFScope dictRef = new(IOKit.IORegistryEntrySearchCFProperty(
            entry, 
            IOServicePlane, 
            cfKey, 
            nint.Zero, 
            IterateRecursively | IterateParents));

        if (dictRef.IsNull) {
            return;
        }

        Dictionary<string, nint> dict = CoreFoundation.ToDictionary(dictRef);
        device.BusType = ReadTrimmedString(dict, "Physical Interconnect") ?? device.BusType;
    }

    private static string? ReadTrimmedString(Dictionary<string, nint> dict, string key)
    {
        if (!dict.TryGetValue(key, out nint value)) {
            return null;
        }

        string? text = CoreFoundation.GetString(value)?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
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
}
#endif
