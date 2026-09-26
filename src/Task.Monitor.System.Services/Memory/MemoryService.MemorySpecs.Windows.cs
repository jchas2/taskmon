using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
 using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Memory;

public partial class MemoryService
{
#if __WIN32__
    private const uint RawSmbiosProvider = 0x5253_4D42;
    private const int  RawSmbiosHeaderLength = 8;
    
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

        specs.Devices.AddRange(MemoryDeviceParser.ParseTable(table));
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

        byte[] table = new byte[written];
        Marshal.Copy(buffer, table, 0, (int)written);
        Marshal.FreeHGlobal(buffer);
        
        return table;
    }
#endif
}