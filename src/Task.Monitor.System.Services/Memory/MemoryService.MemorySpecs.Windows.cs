using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
 using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Memory;

public partial class MemoryService
{
#if __WIN32__
    private const int  BytesPerMegabyte      = 1024 * 1024;
    private const uint RawSmbiosProvider     = 0x5253_4D42;
    private const int  RawSmbiosHeaderLength = 8;

    private void OnStartMemorySpecs(MemorySpecs specs)
    {
        byte[]? rawData = ReadRawSmbiosTable();

        if (rawData != null) {
            specs.Devices.AddRange(ParseRawSmbiosTable(rawData));

            // The raw table's header holds the SMBIOS version at bytes 1 and 2.
            Trace.WriteLine(
                $"{ServiceName}: SMBIOS {rawData[1]}.{rawData[2]} table OK, {rawData.Length} bytes, " +
                $"{specs.Devices.Count} memory devices");
        }

        // Some virtual machines, and old firmware, list no memory devices.
        // Fallback to a mock device displaying the total system memory.
        if (specs.Devices.Count == 0 && TryGetTotalPhysicalMemory(out ulong totalBytes)) {
            specs.Devices.Add(SystemMemoryDevice(totalBytes));
            Trace.WriteLine($"{ServiceName}: no SMBIOS memory devices, showing the total Windows reports instead");
        }
    }

    private static List<MemoryDevice> ParseRawSmbiosTable(byte[] rawData)
    {
        uint tableLength = BinaryPrimitives.ReadUInt32LittleEndian(rawData.AsSpan(4));

        int available = rawData.Length - RawSmbiosHeaderLength;
        int length    = (int)Math.Min(tableLength, (uint)Math.Max(available, 0));

        ReadOnlySpan<byte> table = rawData.AsSpan(RawSmbiosHeaderLength, length);

        return MemoryDeviceParser.ParseTable(table);
    }

    private static byte[]? ReadRawSmbiosTable()
    {
        uint size = Kernel32.GetSystemFirmwareTable(
            RawSmbiosProvider, 
            firmwareTableId: 0, 
            pFirmwareTableBuffer: 0, 
            bufferSize: 0);
        
        if (size == 0) {
            Trace.WriteLine(
                $"{nameof(MemoryService)}: SMBIOS {nameof(Kernel32.GetSystemFirmwareTable)} FAILED " +
                $"{ErrorCodes.Describe(Marshal.GetLastPInvokeError())}");

            return null;
        }

        using HGlobalScope buffer = HGlobalScope.Allocate((int)size);

        uint written = Kernel32.GetSystemFirmwareTable(
            RawSmbiosProvider,
            firmwareTableId: 0,
            buffer,
            size);

        if (written == 0 || written > size) {
            Trace.WriteLine(
                $"{nameof(MemoryService)}: SMBIOS {nameof(Kernel32.GetSystemFirmwareTable)} FAILED " +
                $"{ErrorCodes.Describe(Marshal.GetLastPInvokeError())}, reading {size} bytes");

            return null;
        }

        if (written < RawSmbiosHeaderLength) {
            Trace.WriteLine($"{nameof(MemoryService)}: SMBIOS table FAILED, only {written} bytes");
            return null;
        }

        byte[] table = new byte[written];
        Marshal.Copy(buffer, table, 0, (int)written);

        return table;
    }

    internal static MemoryDevice SystemMemoryDevice(ulong totalBytes) => new() {
        SizeInMegabytes      = (uint)(totalBytes / BytesPerMegabyte),
        Speed                = 0,
        ConfiguredClockSpeed = 0,
        Slot                 = 0,
        MemoryType           = "Unknown",
        FormFactor           = "Unknown",
        DeviceLocator        = "System Memory",
        BankLocator          = "N/A",
        Manufacturer         = "N/A",
        SerialNumber         = "N/A",
        PartNumber           = "N/A"
    };

    private static unsafe bool TryGetTotalPhysicalMemory(out ulong totalBytes)
    {
        SysInfoApi.MEMORYSTATUSEX memoryStatus = new();

        if (!SysInfoApi.GlobalMemoryStatusEx(&memoryStatus)) {
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(SysInfoApi.GlobalMemoryStatusEx));
            totalBytes = 0;
            return false;
        }

        totalBytes = memoryStatus.ullTotalPhys;
        return true;
    }
#endif
}
