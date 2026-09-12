using System.Buffers.Binary;
using System.Text;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Disk;

public static class DiskDeviceParser
{
    public const string NotAvailable = "N/A";

    public static string DecodeBusType(uint busType) => busType switch
    {
        WinIoCtl.BusTypeNvme              => "NVMe",
        WinIoCtl.BusTypeSata              => "SATA",
        WinIoCtl.BusTypeAta               => "ATA",
        WinIoCtl.BusTypeAtapi             => "ATAPI",
        WinIoCtl.BusTypeScsi              => "SCSI",
        WinIoCtl.BusTypeSas               => "SAS",
        WinIoCtl.BusTypeUsb               => "USB",
        WinIoCtl.BusTypeRAID              => "RAID",
        WinIoCtl.BusTypeSd                => "SD",
        WinIoCtl.BusTypeMmc               => "MMC",
        WinIoCtl.BusType1394              => "IEEE 1394",
        WinIoCtl.BusTypeFibre             => "Fibre Channel",
        WinIoCtl.BusTypeiScsi             => "iSCSI",
        WinIoCtl.BusTypeSsa               => "SSA",
        WinIoCtl.BusTypeVirtual           => "Virtual",
        WinIoCtl.BusTypeFileBackedVirtual => "Virtual (File Backed)",
        WinIoCtl.BusTypeSpaces            => "Storage Spaces",
        WinIoCtl.BusTypeSCM               => "SCM",
        WinIoCtl.BusTypeUfs               => "UFS",
        _                                 => NotAvailable,
    };

    public static string DecodeDriveType(uint driveType) => driveType switch
    {
        FileApi.DRIVE_FIXED       => "Fixed",
        FileApi.DRIVE_REMOVABLE   => "Removable",
        FileApi.DRIVE_CDROM       => "CD-ROM",
        FileApi.DRIVE_REMOTE      => "Network",
        FileApi.DRIVE_RAMDISK     => "RAM Disk",
        FileApi.DRIVE_NO_ROOT_DIR => "No Root Directory",
        _                         => NotAvailable,
    };

    // Decodes the fixed header of a STORAGE_DEVICE_DESCRIPTOR. The four string fields are byte
    // offsets into the same buffer pointing at NUL terminated ASCII, so the whole descriptor has
    // to stay intact rather than being marshalled into a fixed size struct.
    public static DiskDevice Parse(ReadOnlySpan<byte> descriptor)
    {
        DiskDevice device = new();

        if (descriptor.Length < WinIoCtl.StorageDeviceDescriptorMinimumLength) {
            return device;
        }

        // Not inline declared to assist with debugging.
        device.IsRemovable      = descriptor[WinIoCtl.StorageDeviceDescriptorRemovableMediaOffset] != 0;
        device.Manufacturer     = GetString(descriptor, WinIoCtl.StorageDeviceDescriptorVendorIdOffset);
        device.Model            = GetString(descriptor, WinIoCtl.StorageDeviceDescriptorProductIdOffset);
        device.FirmwareRevision = GetString(descriptor, WinIoCtl.StorageDeviceDescriptorProductRevisionOffset);
        device.SerialNumber     = GetString(descriptor, WinIoCtl.StorageDeviceDescriptorSerialNumberOffset);
        device.BusType          = DecodeBusType(
            BinaryPrimitives.ReadUInt32LittleEndian(descriptor[WinIoCtl.StorageDeviceDescriptorBusTypeOffset..]));

        return device;
    }

    // NVMe is solid state by definition. Otherwise the seek penalty descriptor is authoritative
    // when the driver answers it, and TRIM support is the fallback for the USB bridges and older
    // controllers that do not.
    public static string DecodeMediaType(
        string busType,
        bool isRemovable,
        bool hasSeekPenalty,
        bool incursSeekPenalty,
        bool hasTrim,
        bool trimEnabled)
    {
        if (busType == "NVMe") {
            return "SSD";
        }

        if (hasSeekPenalty) {
            return incursSeekPenalty ? "HDD" : "SSD";
        }

        if (hasTrim && trimEnabled) {
            return "SSD";
        }

        return isRemovable ? "Removable" : NotAvailable;
    }

    private static string GetString(ReadOnlySpan<byte> descriptor, int fieldOffset)
    {
        uint stringOffset = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[fieldOffset..]);

        // Zero means the device did not supply this field.
        if (stringOffset == 0 || stringOffset >= (uint)descriptor.Length) {
            return NotAvailable;
        }

        ReadOnlySpan<byte> value = descriptor[(int)stringOffset..];
        int terminator = value.IndexOf((byte)0);

        if (terminator >= 0) {
            value = value[..terminator];
        }

        string text = Encoding.Latin1.GetString(value).Trim();

        return text.Length > 0 ? text : NotAvailable;
    }
}
