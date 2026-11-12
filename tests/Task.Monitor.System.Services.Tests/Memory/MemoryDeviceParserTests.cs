using System.Buffers.Binary;
using System.Text;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.System.Services.Tests.Memory;

public sealed class MemoryDeviceParserTests
{
    private const byte MemoryDeviceType = 17;
    private const int  MemoryDeviceLength = 0x28;

    private const string NotAvailable = "N/A";

    [Fact]
    public void MemoryDevice_Canary_Test() =>
        Assert.Equal(11, CanaryTestHelper.GetPropertyCount<MemoryDevice>());

    // A Type 17 structure for a 16 GB DDR5 DIMM, 4800 MT/s configured at 4400, with no serial
    // number. withStrings: false leaves every string index at 0 and an empty string table. A
    // shorter formattedLength builds an older structure, without the fields that came later.
    private static byte[] MemoryDeviceStructure(
        string deviceLocator = "DIMM 0",
        ushort size = 16384,
        uint extendedSize = 0,
        int formattedLength = MemoryDeviceLength,
        bool withStrings = true,
        byte formFactor = 0x09,
        byte memoryType = 0x22)
    {
        string[] strings = withStrings ? [deviceLocator, "BANK 0", "Kingston", "KF548C38"] : [];

        return Structure(MemoryDeviceType, formattedLength, strings, formatted => {
            BinaryPrimitives.WriteUInt16LittleEndian(formatted.AsSpan(0x0C), size);
            formatted[0x0E] = formFactor;
            formatted[0x12] = memoryType;

            if (withStrings) {
                formatted[0x10] = 1;                                        // Device locator
                formatted[0x11] = 2;                                        // Bank locator
            }

            if (formattedLength >= 0x17) {
                BinaryPrimitives.WriteUInt16LittleEndian(formatted.AsSpan(0x15), 4800);
            }

            if (formattedLength >= 0x1B && withStrings) {
                formatted[0x17] = 3;                                        // Manufacturer
                formatted[0x1A] = 4;                                        // Part number
            }

            if (formattedLength >= 0x22) {
                BinaryPrimitives.WriteUInt32LittleEndian(formatted.AsSpan(0x1C), extendedSize);
                BinaryPrimitives.WriteUInt16LittleEndian(formatted.AsSpan(0x20), 4400);
            }
        });
    }

    [Fact]
    public void ParseTable_Decodes_A_Device_Whose_String_Table_Is_Cut_Off()
    {
        byte[] full = MemoryDeviceStructure("DIMM 0");

        // Cut inside "Kingston": no terminator for it, and no closing double NUL.
        int cut = MemoryDeviceLength + "DIMM 0\0".Length + "BANK 0\0".Length + "Kingst".Length;
        byte[] table = full[..cut];

        List<MemoryDevice> devices = MemoryDeviceParser.ParseTable(table);

        MemoryDevice device = Assert.Single(devices);
        Assert.Equal("DIMM 0", device.DeviceLocator);
        Assert.Equal("BANK 0", device.BankLocator);
        Assert.Equal(NotAvailable, device.Manufacturer);
        Assert.Equal(NotAvailable, device.PartNumber);
    }

    [Fact]
    public void ParseTable_Decodes_Every_Memory_Device_And_Skips_Other_Structures()
    {
        byte[] table = Table(
            Structure(0, 0x18, ["American Megatrends", "1.0"]),     // BIOS information
            MemoryDeviceStructure("DIMM 0"),
            Structure(16, 0x17, []),                                // Physical memory array
            MemoryDeviceStructure("DIMM 1"),
            Structure(127, 4, []));                                 // End of table

        List<MemoryDevice> devices = MemoryDeviceParser.ParseTable(table);

        Assert.Equal(2, devices.Count);

        MemoryDevice first = devices[0];
        Assert.Equal(1, first.Slot);
        Assert.Equal(16384u, first.SizeInMegabytes);
        Assert.Equal("DDR5", first.MemoryType);
        Assert.Equal("DIMM", first.FormFactor);
        Assert.Equal(4800, first.Speed);
        Assert.Equal(4400, first.ConfiguredClockSpeed);
        Assert.Equal("DIMM 0", first.DeviceLocator);
        Assert.Equal("BANK 0", first.BankLocator);
        Assert.Equal("Kingston", first.Manufacturer);
        Assert.Equal(NotAvailable, first.SerialNumber);
        Assert.Equal("KF548C38", first.PartNumber);

        Assert.Equal(2, devices[1].Slot);
        Assert.Equal("DIMM 1", devices[1].DeviceLocator);
    }

    // An older structure keeps the fields it has. Those added by later versions read as unknown.
    [Theory]
    [InlineData(0x15, (ushort)0,    NotAvailable, (ushort)0)]      // SMBIOS 2.1
    [InlineData(0x1B, (ushort)4800, "Kingston",   (ushort)0)]      // SMBIOS 2.3
    [InlineData(0x22, (ushort)4800, "Kingston",   (ushort)4400)]   // SMBIOS 2.7
    public void ParseTable_Decodes_The_Fields_An_Older_Memory_Device_Holds(
        int formattedLength,
        ushort expectedSpeed,
        string expectedManufacturer,
        ushort expectedConfiguredSpeed)
    {
        byte[] table = MemoryDeviceStructure("DIMM 0", formattedLength: formattedLength);

        MemoryDevice device = Assert.Single(MemoryDeviceParser.ParseTable(table));

        Assert.Equal(16384u, device.SizeInMegabytes);
        Assert.Equal("DDR5", device.MemoryType);
        Assert.Equal("DIMM", device.FormFactor);
        Assert.Equal("DIMM 0", device.DeviceLocator);
        Assert.Equal("BANK 0", device.BankLocator);
        Assert.Equal(expectedSpeed, device.Speed);
        Assert.Equal(expectedManufacturer, device.Manufacturer);
        Assert.Equal(expectedConfiguredSpeed, device.ConfiguredClockSpeed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ParseTable_Returns_No_Devices_For_A_Table_Too_Short_For_A_Structure_Header(int length) =>
        Assert.Empty(MemoryDeviceParser.ParseTable(new byte[length]));

    // SMBIOS 2.1 introduced Type 17 at 0x15 bytes. A shorter one is skipped, and does not take a
    // slot number.
    [Fact]
    public void ParseTable_Skips_A_Memory_Device_Shorter_Than_The_Minimum_Length()
    {
        byte[] table = Table(
            MemoryDeviceStructure("BROKEN", formattedLength: 0x14),
            MemoryDeviceStructure("DIMM 0"));

        List<MemoryDevice> devices = MemoryDeviceParser.ParseTable(table);

        MemoryDevice device = Assert.Single(devices);
        Assert.Equal(1, device.Slot);
        Assert.Equal("DIMM 0", device.DeviceLocator);
    }

    [Fact]
    public void ParseTable_Stops_At_A_Formatted_Length_Below_The_Structure_Header()
    {
        byte[] table = Table(
            MemoryDeviceStructure("DIMM 0"),
            Structure(5, 2, []),
            MemoryDeviceStructure("DIMM 1"));

        List<MemoryDevice> devices = MemoryDeviceParser.ParseTable(table);

        Assert.Equal("DIMM 0", Assert.Single(devices).DeviceLocator);
    }

    [Fact]
    public void ParseTable_Stops_When_A_Formatted_Area_Runs_Past_The_End_Of_The_Table()
    {
        // A header claiming a full Type 17 formatted area, with only 10 bytes of it present.
        byte[] truncated = [MemoryDeviceType, MemoryDeviceLength, 0, 0, 0, 0, 0, 0, 0, 0];

        byte[] table = Table(MemoryDeviceStructure("DIMM 0"), truncated);

        List<MemoryDevice> devices = MemoryDeviceParser.ParseTable(table);

        Assert.Equal("DIMM 0", Assert.Single(devices).DeviceLocator);
    }

    [Fact]
    public void ParseTable_Walks_Past_A_Structure_With_An_Empty_String_Table()
    {
        byte[] table = Table(
            MemoryDeviceStructure(withStrings: false),
            MemoryDeviceStructure("DIMM 1"));

        List<MemoryDevice> devices = MemoryDeviceParser.ParseTable(table);

        Assert.Equal(2, devices.Count);
        Assert.Equal(NotAvailable, devices[0].DeviceLocator);
        Assert.Equal(NotAvailable, devices[0].Manufacturer);
        Assert.Equal("DIMM 1", devices[1].DeviceLocator);
    }

    // Before SMBIOS 2.7 there is no extended size, so 0x7FFF is a size like any other.
    [Fact]
    public void Parse_Decodes_0x7FFF_As_Megabytes_In_A_Structure_Without_An_Extended_Size()
    {
        MemoryDevice device = MemoryDeviceParser.Parse(MemoryDeviceStructure(size: 0x7FFF, formattedLength: 0x1B));

        Assert.Equal(32767u, device.SizeInMegabytes);
    }

    [Theory]
    [InlineData((byte)0x09, "DIMM")]
    [InlineData((byte)0x0B, "Row of Chips")]
    [InlineData((byte)0x0D, "SODIMM")]
    [InlineData((byte)0x0F, "FB-DIMM")]
    [InlineData((byte)0x11, "CAMM")]
    [InlineData((byte)0x02, "Unknown")]
    [InlineData((byte)0x00, "Unknown")]
    [InlineData((byte)0xFF, "Unknown")]
    public void Parse_Decodes_The_Form_Factor(byte formFactor, string expected) =>
        Assert.Equal(expected, MemoryDeviceParser.Parse(MemoryDeviceStructure(formFactor: formFactor)).FormFactor);

    [Theory]
    [InlineData((byte)0x0F, "SDRAM")]
    [InlineData((byte)0x12, "DDR")]
    [InlineData((byte)0x18, "DDR3")]
    [InlineData((byte)0x1A, "DDR4")]
    [InlineData((byte)0x1E, "LPDDR4")]
    [InlineData((byte)0x22, "DDR5")]
    [InlineData((byte)0x23, "LPDDR5")]
    [InlineData((byte)0x02, "Unknown")]
    [InlineData((byte)0x16, "Unknown")]                             // Reserved.
    [InlineData((byte)0xFF, "Unknown")]
    public void Parse_Decodes_The_Memory_Type(byte memoryType, string expected) =>
        Assert.Equal(expected, MemoryDeviceParser.Parse(MemoryDeviceStructure(memoryType: memoryType)).MemoryType);

    [Theory]
    [InlineData((ushort)16384, 0u, 16384u)]         // Megabytes.
    [InlineData((ushort)(0x8000 | 2048), 0u, 2u)]   // Kilobytes, flagged by bit 15.
    [InlineData((ushort)0x7FFF, 65536u, 65536u)]    // Too large for the word: extended size.
    [InlineData((ushort)0, 0u, 0u)]                 // No module installed.
    [InlineData((ushort)0xFFFF, 0u, 0u)]            // Size unknown.
    public void Parse_Decodes_The_Module_Size(ushort size, uint extendedSize, uint expectedMegabytes)
    {
        MemoryDevice device = MemoryDeviceParser.Parse(MemoryDeviceStructure(size: size, extendedSize: extendedSize));

        Assert.Equal(expectedMegabytes, device.SizeInMegabytes);
    }

    // An SMBIOS string table: each string NUL-terminated, then a closing NUL. A structure with no
    // strings still ends in a double NUL.
    private static byte[] StringTable(string[] strings) =>
        strings.Length == 0
            ? [0, 0]
            : [.. strings.SelectMany(value => Encoding.Latin1.GetBytes(value + '\0')), 0];

    private static byte[] Structure(byte type, int formattedLength, string[] strings, Action<byte[]>? fill = null)
    {
        byte[] formatted = new byte[formattedLength];
        formatted[0] = type;
        formatted[1] = (byte)formattedLength;
        fill?.Invoke(formatted);

        return [.. formatted, .. StringTable(strings)];
    }

    private static byte[] Table(params byte[][] structures) => [.. structures.SelectMany(structure => structure)];
}
