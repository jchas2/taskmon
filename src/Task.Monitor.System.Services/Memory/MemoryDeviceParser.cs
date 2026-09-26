using System.Buffers.Binary;
using System.Text;

namespace Task.Monitor.System.Services.Memory;

public static class MemoryDeviceParser
{
    private const int  StructureHeaderLength     = 4;
    private const byte MemoryDeviceType          = 17;
    private const int  MemoryDeviceMinimumLength = 0x28;

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

    private static string DecodeFormFactor(byte formFactor) => formFactor switch
    {
        0x09 => "DIMM",
        0x0F => "SODIMM",
        0x08 => "Row of Chips",
        _    => "Unknown",
    };

    private static string DecodeMemoryType(byte type) => type switch
    {
        0x1A => "DDR4",
        0x22 => "DDR5",
        0x18 => "DDR3",
        0x13 => "DDR2",
        0x0F => "DDR",
        0x09 => "SO-DIMM / SDRAM",
        _    => "Unknown/Other",
    };

    private static uint DecodeSize(ReadOnlySpan<byte> structure)
    {
        ushort size = BinaryPrimitives.ReadUInt16LittleEndian(structure[SizeOffset..]);

        // Unknown device.
        if (size is 0 or 0xFFFF) {
            return 0;
        }

        // Uses extended size.
        if (size == 0x7FFF) {
            return BinaryPrimitives.ReadUInt32LittleEndian(structure[ExtendedSizeOffset..]) & 0x7FFF_FFFF;
        }

        return (size & 0x8000) == 0
            ? (uint)size                    // Already Megabytes.
            : (uint)(size & 0x7FFF) / 1024; // Kilobytes.
    }

    // Walks a SMBIOS structure table - the raw firmware table past its 8 byte header - and
    // decodes every Type 17 (Memory Device) structure, numbering them as slots in table order.
    public static List<MemoryDevice> ParseTable(ReadOnlySpan<byte> table)
    {
        List<MemoryDevice> devices = new();
        int cursor = 0;

        while (cursor + StructureHeaderLength <= table.Length) {
            byte type = table[cursor];
            byte formattedLength = table[cursor + 1];

            // Avoid spinning forever on a corrupted table.
            if (formattedLength < StructureHeaderLength || cursor + formattedLength > table.Length) {
                break;
            }

            int stringTableStart = cursor + formattedLength;
            int ptr = stringTableStart;

            // Each structure is terminated with a double null.
            while (ptr + 1 < table.Length && !(table[ptr] == 0 && table[ptr + 1] == 0)) {
                ptr++;
            }

            int end = Math.Min(ptr + 2, table.Length);

            // Decode the Type 17 Memory Device structure.
            if (type == MemoryDeviceType && formattedLength >= MemoryDeviceMinimumLength) {
                MemoryDevice device = Parse(table[cursor..end]);
                device.Slot = devices.Count + 1;
                devices.Add(device);
            }

            cursor = end;
        }

        return devices;
    }

    public static MemoryDevice Parse(ReadOnlySpan<byte> structure)
    {
        MemoryDevice device = new();
        // Not inline declared to assist with debugging.
        device.SizeInMegabytes      = DecodeSize(structure);
        device.MemoryType           = DecodeMemoryType(structure[MemoryTypeOffset]);
        device.FormFactor           = DecodeFormFactor(structure[FormFactorOffset]);
        device.Speed                = BinaryPrimitives.ReadUInt16LittleEndian(structure[SpeedOffset..]);
        device.ConfiguredClockSpeed = BinaryPrimitives.ReadUInt16LittleEndian(structure[ConfiguredClockSpeedOffset..]);
        device.DeviceLocator        = GetString(structure, structure[DeviceLocatorOffset]);
        device.BankLocator          = GetString(structure, structure[BankLocatorOffset]);
        device.Manufacturer         = GetString(structure, structure[ManufacturerOffset]);
        device.SerialNumber         = GetString(structure, structure[SerialNumberOffset]);
        device.PartNumber           = GetString(structure, structure[PartNumberOffset]);
        
        return device;
    }

    private static string GetString(ReadOnlySpan<byte> structure, byte stringIndex)
    {
        const string NotAvailable = "N/A";

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
}