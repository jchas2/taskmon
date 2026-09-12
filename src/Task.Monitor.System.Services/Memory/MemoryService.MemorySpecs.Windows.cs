using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Memory;

public partial class MemoryService
{
#if __WIN32__
    private const uint RawSmbiosProvider = 0x5253_4D42;
    private const int RawSmbiosHeaderLength = 8;
    private const int Type17 = 17;
    private const int Type17Length = 0x28;              
    
    private void OnStartMemorySpecs(MemorySpecs specs)
    {
        byte[]? rawData = ReadRawSmbiosTable();
        if (rawData == null) {
            return;
        }
        
        byte majorVersion = rawData[1];
        byte minorVersion = rawData[2];
        uint tableLength = BinaryPrimitives.ReadUInt32LittleEndian(rawData.AsSpan(4));
        
        int available = rawData.Length - RawSmbiosHeaderLength;
        int length = (int)Math.Min(tableLength, (uint)Math.Max(available, 0));
        ReadOnlySpan<byte> table = rawData.AsSpan(RawSmbiosHeaderLength, length);

        int slotCount = 0;
        int cursor = 0;

        while (cursor + 4 <= table.Length)
        {
            byte type = table[cursor];
            byte formattedLength = table[cursor + 1];

            // Bail out on a zero length to avoid spinning forever on a corrupted table.
            if (formattedLength < 4 || cursor + formattedLength > table.Length) {
                break;
            }

            int end = FindStructureEnd(table, cursor + formattedLength);
            
            if (type == Type17 && formattedLength >= Type17Length) {
                MemoryDevice device = MemoryDeviceParser.Parse(table[cursor..end]);
                device.Slot = ++slotCount;
                specs.Devices.Add(device);
            }

            cursor = end;
        }
    }
    
    private static int FindStructureEnd(ReadOnlySpan<byte> table, int stringTableStart)
    {
        int i = stringTableStart;
        
        while (i + 1 < table.Length && !(table[i] == 0 && table[i + 1] == 0)) {
            i++;
        }

        return Math.Min(i + 2, table.Length);
    }

    private static byte[]? ReadRawSmbiosTable()
    {
        uint size = Kernel32.GetSystemFirmwareTable(
            RawSmbiosProvider, 
            firmwareTableId: 0, 
            pFirmwareTableBuffer: 0, 
            bufferSize: 0);
        
        if (size == 0) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(Kernel32.GetSystemFirmwareTable));
            return null;
        }

        nint buffer = Marshal.AllocHGlobal((int)size);

        uint written = Kernel32.GetSystemFirmwareTable(
            RawSmbiosProvider, 
            firmwareTableId: 0, 
            buffer, 
            size);
        
        if (written == 0 || written > size) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(Kernel32.GetSystemFirmwareTable), 
                "Failed to retrieve SMBIOS firmware table");
            Marshal.FreeHGlobal(buffer);
            return null;
        }

        if (written < RawSmbiosHeaderLength) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(Kernel32.GetSystemFirmwareTable), 
                "SMBIOS firmware table is truncated");
            Marshal.FreeHGlobal(buffer);
            return null;
        }

        byte[] managed = new byte[written];
        Marshal.Copy(buffer, managed, 0, (int)written);
        Marshal.FreeHGlobal(buffer);
        
        return managed;
    }
#endif
}