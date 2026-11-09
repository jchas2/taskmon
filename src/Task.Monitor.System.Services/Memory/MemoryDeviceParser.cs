using System.Buffers.Binary;
using System.Text;

namespace Task.Monitor.System.Services.Memory;

public static class MemoryDeviceParser
{
    private const string NotAvailable = "N/A";
    private const string Unknown      = "Unknown";

    private const int  StructureHeaderLength = 4;
    private const byte MemoryDeviceType      = 17;

    // SMBIOS 2.1 introduced Type 17 at this length. All later versions append fields.
    private const int MemoryDeviceMinimumLength = 0x15;

    private const int SizeOffset                 = 0x0C;
    private const int FormFactorOffset           = 0x0E;
    private const int DeviceLocatorOffset        = 0x10;
    private const int BankLocatorOffset          = 0x11;
    private const int MemoryTypeOffset           = 0x12;
    private const int SpeedOffset                = 0x15;
    private const int ManufacturerOffset         = 0x17;
    private const int SerialNumberOffset         = 0x18;
    private const int PartNumberOffset           = 0x1A;
    private const int ExtendedSizeOffset         = 0x1C;
    private const int ConfiguredClockSpeedOffset = 0x20;

    // SMBIOS reference specification (DSP0134), Type 17 form factor.
    private static string DecodeFormFactor(byte formFactor) => formFactor switch
    {
        0x01 => "Other",
        0x03 => "SIMM",
        0x04 => "SIP",
        0x05 => "Chip",
        0x06 => "DIP",
        0x07 => "ZIP",
        0x08 => "Proprietary Card",
        0x09 => "DIMM",
        0x0A => "TSOP",
        0x0B => "Row of Chips",
        0x0C => "RIMM",
        0x0D => "SODIMM",
        0x0E => "SRIMM",
        0x0F => "FB-DIMM",
        0x10 => "Die",
        0x11 => "CAMM",
        0x12 => "CUDIMM",
        0x13 => "CSODIMM",
        _    => Unknown,
    };

    // SMBIOS reference specification (DSP0134), Type 17 memory type.
    // 0x15 to 0x17 are reserved.
    private static string DecodeMemoryType(byte type) => type switch
    {
        0x01 => "Other",
        0x03 => "DRAM",
        0x04 => "EDRAM",
        0x05 => "VRAM",
        0x06 => "SRAM",
        0x07 => "RAM",
        0x08 => "ROM",
        0x09 => "Flash",
        0x0A => "EEPROM",
        0x0B => "FEPROM",
        0x0C => "EPROM",
        0x0D => "CDRAM",
        0x0E => "3DRAM",
        0x0F => "SDRAM",
        0x10 => "SGRAM",
        0x11 => "RDRAM",
        0x12 => "DDR",
        0x13 => "DDR2",
        0x14 => "DDR2 FB-DIMM",
        0x18 => "DDR3",
        0x19 => "FBD2",
        0x1A => "DDR4",
        0x1B => "LPDDR",
        0x1C => "LPDDR2",
        0x1D => "LPDDR3",
        0x1E => "LPDDR4",
        0x1F => "Non-volatile",
        0x20 => "HBM",
        0x21 => "HBM2",
        0x22 => "DDR5",
        0x23 => "LPDDR5",
        0x24 => "HBM3",
        0x25 => "MRDIMM",
        _    => Unknown,
    };

    private static uint DecodeSize(ReadOnlySpan<byte> structure)
    {
        ushort size = ReadWord(structure, SizeOffset);

        // No module installed, or the size is unknown.
        if (size is 0 or 0xFFFF) {
            return 0;
        }

        // From SMBIOS 2.7, 0x7FFF means the size is too large for the word and is held in the
        // extended size. An older structure has no extended size, so there it is 32767 MB.
        if (size == 0x7FFF && HasField(structure, ExtendedSizeOffset, sizeof(uint))) {
            return BinaryPrimitives.ReadUInt32LittleEndian(structure[ExtendedSizeOffset..]) & 0x7FFF_FFFF;
        }

        return (size & 0x8000) == 0
            ? (uint)size                    // Already Megabytes.
            : (uint)(size & 0x7FFF) / 1024; // Kilobytes.
    }

    private static string GetString(ReadOnlySpan<byte> structure, int offset)
    {
        if (!HasField(structure, offset, sizeof(byte))) {
            return NotAvailable;
        }

        byte stringIndex = structure[offset];

        if (stringIndex == 0) {
            return NotAvailable;
        }

        ReadOnlySpan<byte> strings = structure[structure[1]..];

        for (byte current = 1; !strings.IsEmpty && strings[0] != 0; current++) {
            int terminator = strings.IndexOf((byte)0);

            if (terminator < 0) {
                break;
            }

            if (current == stringIndex) {
                return Encoding.Latin1.GetString(strings[..terminator]).Trim();
            }

            strings = strings[(terminator + 1)..];
        }

        return NotAvailable;
    }

    private static bool HasField(ReadOnlySpan<byte> structure, int offset, int size) =>
        offset + size <= structure[1];

    public static MemoryDevice Parse(ReadOnlySpan<byte> structure)
    {
        MemoryDevice device = new();
        // Not inline declared to assist with debugging.
        device.SizeInMegabytes      = DecodeSize(structure);
        device.MemoryType           = DecodeMemoryType(structure[MemoryTypeOffset]);
        device.FormFactor           = DecodeFormFactor(structure[FormFactorOffset]);
        device.Speed                = ReadWord(structure, SpeedOffset);
        device.ConfiguredClockSpeed = ReadWord(structure, ConfiguredClockSpeedOffset);
        device.DeviceLocator        = GetString(structure, DeviceLocatorOffset);
        device.BankLocator          = GetString(structure, BankLocatorOffset);
        device.Manufacturer         = GetString(structure, ManufacturerOffset);
        device.SerialNumber         = GetString(structure, SerialNumberOffset);
        device.PartNumber           = GetString(structure, PartNumberOffset);

        return device;
    }

    // Walks a SMBIOS structure table - the raw firmware table past its 8 byte header - and
    // decodes every Type 17 (Memory Device) structure, numbering them as slots in table order.
    public static List<MemoryDevice> ParseTable(ReadOnlySpan<byte> table)
    {
        List<MemoryDevice> devices = new();
        int cursor = 0;

        while (cursor + StructureHeaderLength <= table.Length) {
            byte type            = table[cursor];
            byte formattedLength = table[cursor + 1];

            // Avoid spinning forever on a corrupted table.
            if (formattedLength < StructureHeaderLength || cursor + formattedLength > table.Length) {
                break;
            }

            int stringTableStart = cursor + formattedLength;
            int ptr              = stringTableStart;

            // Each structure is terminated with a double null.
            while (ptr + 1 < table.Length && !(table[ptr] == 0 && table[ptr + 1] == 0)) {
                ptr++;
            }

            int end = Math.Min(ptr + 2, table.Length);

            // Decode the Type 17 Memory Device structure.
            if (type == MemoryDeviceType && formattedLength >= MemoryDeviceMinimumLength) {
                MemoryDevice device = Parse(table[cursor..end]);
                device.Slot         = devices.Count + 1;

                devices.Add(device);
            }

            cursor = end;
        }

        return devices;
    }

    private static ushort ReadWord(ReadOnlySpan<byte> structure, int offset) =>
        HasField(structure, offset, sizeof(ushort))
            ? BinaryPrimitives.ReadUInt16LittleEndian(structure[offset..])
            : (ushort)0;
}
