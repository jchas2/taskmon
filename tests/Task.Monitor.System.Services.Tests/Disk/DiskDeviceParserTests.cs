using System.Buffers.Binary;
using System.Text;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Tests.Disk;

public sealed class DiskDeviceParserTests
{
    private const int HeaderLength           = 0x24;
    private const int RemovableMediaOffset   = 0x0A;
    private const int VendorIdOffset         = 0x0C;
    private const int ProductIdOffset        = 0x10;
    private const int ProductRevisionOffset  = 0x14;
    private const int SerialNumberOffset     = 0x18;
    private const int BusTypeOffset          = 0x1C;

    // Builds a STORAGE_DEVICE_DESCRIPTOR: a fixed header whose four string fields hold byte
    // offsets into the same buffer, each pointing at NUL terminated ASCII appended after it.
    private static byte[] BuildDescriptor(
        string? vendorId,
        string? productId,
        string? productRevision,
        string? serialNumber,
        uint busType,
        bool removableMedia)
    {
        List<byte> strings = new();

        uint Append(string? value)
        {
            if (value == null) {
                return 0;
            }

            uint offset = (uint)(HeaderLength + strings.Count);
            strings.AddRange(Encoding.Latin1.GetBytes(value));
            strings.Add(0);

            return offset;
        }

        uint vendorOffset = Append(vendorId);
        uint productOffset = Append(productId);
        uint revisionOffset = Append(productRevision);
        uint serialOffset = Append(serialNumber);

        byte[] descriptor = new byte[HeaderLength + strings.Count];

        descriptor[RemovableMediaOffset] = removableMedia ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(VendorIdOffset), vendorOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(ProductIdOffset), productOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(ProductRevisionOffset), revisionOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(SerialNumberOffset), serialOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(BusTypeOffset), busType);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(0x04), (uint)descriptor.Length);

        strings.CopyTo(descriptor, HeaderLength);

        return descriptor;
    }

    [Fact]
    public void Should_Parse_A_Complete_Descriptor()
    {
        byte[] descriptor = BuildDescriptor(
            "Samsung",
            "SSD 990 PRO 2TB",
            "4B2QJXD7",
            "S7HENJ0X912345",
            WinIoCtl.BusTypeNvme,
            removableMedia: false);

        DiskDevice device = DiskDeviceParser.Parse(descriptor);

        Assert.Equal("Samsung", device.Manufacturer);
        Assert.Equal("SSD 990 PRO 2TB", device.Model);
        Assert.Equal("4B2QJXD7", device.FirmwareRevision);
        Assert.Equal("S7HENJ0X912345", device.SerialNumber);
        Assert.Equal("NVMe", device.BusType);
        Assert.False(device.IsRemovable);
    }

    [Fact]
    public void Should_Trim_The_Padding_Drives_Pad_Their_Fields_With()
    {
        byte[] descriptor = BuildDescriptor(
            "ATA     ",
            "  ST2000DM008-2FR102  ",
            "0001",
            "  WCC6Y1234567 ",
            WinIoCtl.BusTypeSata,
            removableMedia: false);

        DiskDevice device = DiskDeviceParser.Parse(descriptor);

        Assert.Equal("ATA", device.Manufacturer);
        Assert.Equal("ST2000DM008-2FR102", device.Model);
        Assert.Equal("WCC6Y1234567", device.SerialNumber);
        Assert.Equal("SATA", device.BusType);
    }

    [Fact]
    public void Should_Report_Not_Available_For_Fields_The_Device_Omits()
    {
        // A zero offset means the device did not supply that field, which is common for the
        // vendor id on consumer NVMe drives.
        byte[] descriptor = BuildDescriptor(
            vendorId: null,
            productId: "WD Blue SN580",
            productRevision: null,
            serialNumber: null,
            WinIoCtl.BusTypeNvme,
            removableMedia: false);

        DiskDevice device = DiskDeviceParser.Parse(descriptor);

        Assert.Equal(DiskDeviceParser.NotAvailable, device.Manufacturer);
        Assert.Equal("WD Blue SN580", device.Model);
        Assert.Equal(DiskDeviceParser.NotAvailable, device.FirmwareRevision);
        Assert.Equal(DiskDeviceParser.NotAvailable, device.SerialNumber);
    }

    [Fact]
    public void Should_Report_Not_Available_For_An_Offset_Past_The_Buffer()
    {
        byte[] descriptor = BuildDescriptor(
            "Kingston",
            null,
            null,
            null,
            WinIoCtl.BusTypeUsb,
            removableMedia: true);

        BinaryPrimitives.WriteUInt32LittleEndian(
            descriptor.AsSpan(ProductIdOffset),
            (uint)descriptor.Length + 64);

        DiskDevice device = DiskDeviceParser.Parse(descriptor);

        Assert.Equal("Kingston", device.Manufacturer);
        Assert.Equal(DiskDeviceParser.NotAvailable, device.Model);
        Assert.Equal("USB", device.BusType);
        Assert.True(device.IsRemovable);
    }

    [Fact]
    public void Should_Not_Read_Past_An_Unterminated_String()
    {
        byte[] descriptor = BuildDescriptor(
            "Seagate",
            null,
            null,
            null,
            WinIoCtl.BusTypeSas,
            removableMedia: false);

        // Drop the NUL that terminates the vendor id so it runs to the end of the buffer.
        byte[] truncated = descriptor[..^1];

        DiskDevice device = DiskDeviceParser.Parse(truncated);

        Assert.Equal("Seagate", device.Manufacturer);
        Assert.Equal("SAS", device.BusType);
    }

    [Fact]
    public void Should_Return_Defaults_For_A_Descriptor_Shorter_Than_Its_Header()
    {
        DiskDevice device = DiskDeviceParser.Parse(new byte[8]);

        Assert.Equal(DiskDeviceParser.NotAvailable, device.Manufacturer);
        Assert.Equal(DiskDeviceParser.NotAvailable, device.Model);
        Assert.Equal(DiskDeviceParser.NotAvailable, device.BusType);
    }

    [Fact]
    public void Should_Report_An_Unknown_Bus_Type_As_Not_Available()
    {
        byte[] descriptor = BuildDescriptor(null, null, null, null, 0x7F, removableMedia: false);

        DiskDevice device = DiskDeviceParser.Parse(descriptor);

        Assert.Equal(DiskDeviceParser.NotAvailable, device.BusType);
    }

    [Theory]
    [InlineData(FileApi.DRIVE_FIXED, "Fixed")]
    [InlineData(FileApi.DRIVE_REMOVABLE, "Removable")]
    [InlineData(FileApi.DRIVE_CDROM, "CD-ROM")]
    [InlineData(FileApi.DRIVE_REMOTE, "Network")]
    [InlineData(FileApi.DRIVE_RAMDISK, "RAM Disk")]
    [InlineData(FileApi.DRIVE_UNKNOWN, DiskDeviceParser.NotAvailable)]
    public void Should_Decode_Drive_Type(uint driveType, string expected) =>
        Assert.Equal(expected, DiskDeviceParser.DecodeDriveType(driveType));

    [Fact]
    public void Should_Treat_Nvme_As_Solid_State_Without_Probing_Further()
    {
        string mediaType = DiskDeviceParser.DecodeMediaType(
            "NVMe",
            isRemovable: false,
            hasSeekPenalty: false,
            incursSeekPenalty: false,
            hasTrim: false,
            trimEnabled: false);

        Assert.Equal("SSD", mediaType);
    }

    [Theory]
    [InlineData(true, "HDD")]
    [InlineData(false, "SSD")]
    public void Should_Prefer_The_Seek_Penalty_Descriptor(bool incursSeekPenalty, string expected)
    {
        string mediaType = DiskDeviceParser.DecodeMediaType(
            "SATA",
            isRemovable: false,
            hasSeekPenalty: true,
            incursSeekPenalty,
            hasTrim: false,
            trimEnabled: false);

        Assert.Equal(expected, mediaType);
    }

    [Fact]
    public void Should_Fall_Back_To_Trim_When_Seek_Penalty_Is_Unavailable()
    {
        string mediaType = DiskDeviceParser.DecodeMediaType(
            "USB",
            isRemovable: false,
            hasSeekPenalty: false,
            incursSeekPenalty: false,
            hasTrim: true,
            trimEnabled: true);

        Assert.Equal("SSD", mediaType);
    }

    [Fact]
    public void Should_Report_Removable_When_The_Device_Answers_Nothing()
    {
        string mediaType = DiskDeviceParser.DecodeMediaType(
            "USB",
            isRemovable: true,
            hasSeekPenalty: false,
            incursSeekPenalty: false,
            hasTrim: false,
            trimEnabled: false);

        Assert.Equal("Removable", mediaType);
    }

    [Fact]
    public void Should_Report_Not_Available_When_Nothing_Identifies_The_Media()
    {
        string mediaType = DiskDeviceParser.DecodeMediaType(
            "SCSI",
            isRemovable: false,
            hasSeekPenalty: false,
            incursSeekPenalty: false,
            hasTrim: false,
            trimEnabled: false);

        Assert.Equal(DiskDeviceParser.NotAvailable, mediaType);
    }
}
