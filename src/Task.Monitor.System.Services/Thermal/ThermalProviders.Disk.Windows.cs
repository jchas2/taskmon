#if __WIN32__
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Thermal;

#pragma warning disable CA1416 // Validate platform compatibility

// NVMe drives: the SMART / Health Information log page carries a composite temperature plus up to
// eight per-sensor readings, and the query runs against the same zero-access handle DiskService
// already opens - no elevation.
internal sealed unsafe class NvmeDiskThermalProvider(Func<IReadOnlyList<DiskDevice>> getDisks) : IThermalProvider
{
    private const int HeaderSize = 8;
    private const int ProtocolDataSize = 40;
    private const int LogSize = 512;
    private const int BufferSize = HeaderSize + ProtocolDataSize + LogSize;

    public string Name => "NVMe SMART";

    public bool TryInitialise() => true;

    public IEnumerable<ThermalSensor> Read()
    {
        foreach (DiskDevice disk in getDisks()) {
            if (!string.Equals(disk.BusType, "NVMe", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            byte[]? log = ReadHealthLog(disk.Index);

            if (log is null) {
                continue;
            }

            string id = disk.Index.ToString();

            if (NvmeHealthLog.CompositeCelsius(log) is { } composite) {
                yield return new ThermalSensor {
                    Component = ThermalComponent.Disk,
                    ComponentId = id,
                    SensorName = "Composite",
                    Celsius = composite,
                    Source = ThermalSource.NvmeHealthLog
                };
            }

            foreach ((int index, double celsius) in NvmeHealthLog.Sensors(log)) {
                yield return new ThermalSensor {
                    Component = ThermalComponent.Disk,
                    ComponentId = id,
                    SensorName = $"Sensor {index}",
                    Celsius = celsius,
                    Source = ThermalSource.NvmeHealthLog
                };
            }
        }
    }

    private static byte[]? ReadHealthLog(int physicalDriveIndex)
    {
        nint handle = FileApi.CreateFileW(
            $@"\\.\PhysicalDrive{physicalDriveIndex}",
            0,
            FileApi.FILE_SHARE_READ | FileApi.FILE_SHARE_WRITE,
            nint.Zero,
            FileApi.OPEN_EXISTING,
            0,
            nint.Zero);

        if (handle == Kernel32.INVALID_HANDLE_VALUE) {
            return null;
        }

        try {
            byte* buffer = stackalloc byte[BufferSize];
            new Span<byte>(buffer, BufferSize).Clear();

            BinaryPrimitives.WriteUInt32LittleEndian(
                new Span<byte>(buffer, 4), WinIoCtl.StorageDeviceProtocolSpecificProperty);
            BinaryPrimitives.WriteUInt32LittleEndian(
                new Span<byte>(buffer + 4, 4), WinIoCtl.PropertyStandardQuery);

            byte* proto = buffer + HeaderSize;
            *(uint*)(proto + WinIoCtl.ProtocolSpecificProtocolTypeOffset) = WinIoCtl.ProtocolTypeNvme;
            *(uint*)(proto + WinIoCtl.ProtocolSpecificDataTypeOffset) = WinIoCtl.NVMeDataTypeLogPage;
            *(uint*)(proto + WinIoCtl.ProtocolSpecificRequestValueOffset) = WinIoCtl.NVMeLogPageHealthInfo;
            *(uint*)(proto + WinIoCtl.ProtocolSpecificDataOffsetOffset) = ProtocolDataSize;
            *(uint*)(proto + WinIoCtl.ProtocolSpecificDataLengthOffset) = LogSize;

            uint returned = 0;

            bool ok = IoApiSet.DeviceIoControl(
                handle,
                WinIoCtl.IOCTL_STORAGE_QUERY_PROPERTY,
                buffer, BufferSize,
                buffer, BufferSize,
                &returned, nint.Zero);

            if (!ok || returned < HeaderSize + ProtocolDataSize) {
                return null;
            }

            byte* responseProto = buffer + HeaderSize;
            uint dataOffset = *(uint*)(responseProto + WinIoCtl.ProtocolSpecificDataOffsetOffset);
            uint dataLength = *(uint*)(responseProto + WinIoCtl.ProtocolSpecificDataLengthOffset);

            int logStart = HeaderSize + (int)dataOffset;

            // Some drivers do not fill the response descriptor; fall back to the standard placement.
            if (dataLength < 216 || logStart < 0 || logStart + 216 > BufferSize) {
                logStart = HeaderSize + ProtocolDataSize;
                dataLength = LogSize;
            }

            int available = Math.Min((int)dataLength, BufferSize - logStart);

            if (available < 216) {
                return null;
            }

            return new Span<byte>(buffer + logStart, available).ToArray();
        }
        finally {
            Kernel32.CloseHandle(handle);
        }
    }

    public void Dispose() { }
}

// SATA / ATA drives: SMART attribute 194. SMART_RCV_DRIVE_DATA needs a GENERIC_READ|GENERIC_WRITE
// handle, which usually needs elevation - a drive that comes back ACCESS_DENIED simply contributes
// no sensor.
internal sealed unsafe class AtaDiskThermalProvider(Func<IReadOnlyList<DiskDevice>> getDisks) : IThermalProvider
{
    private const uint SMART_RCV_DRIVE_DATA = 0x0007C088;
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const int ERROR_ACCESS_DENIED = 5;

    private const int SendCmdInParamsHeaderSize = 32;
    private const int SendCmdOutParamsHeaderSize = 16;
    private const int SmartDataSize = 512;

    private bool accessDenied;

    public string Name => "ATA SMART";

    public bool TryInitialise() => true;

    public IEnumerable<ThermalSensor> Read()
    {
        // Once one drive has refused for lack of privilege, the rest will too; stop trying.
        if (accessDenied) {
            yield break;
        }

        foreach (DiskDevice disk in getDisks()) {
            if (!IsAtaBus(disk.BusType)) {
                continue;
            }

            double? celsius = ReadSmartTemperature(disk.Index);

            if (celsius is { } value) {
                yield return new ThermalSensor {
                    Component = ThermalComponent.Disk,
                    ComponentId = disk.Index.ToString(),
                    SensorName = "Drive",
                    Celsius = value,
                    Source = ThermalSource.AtaSmart
                };
            }

            if (accessDenied) {
                yield break;
            }
        }
    }

    private static bool IsAtaBus(string busType) =>
        busType is "SATA" or "ATA" or "ATAPI";

    private double? ReadSmartTemperature(int physicalDriveIndex)
    {
        nint handle = FileApi.CreateFileW(
            $@"\\.\PhysicalDrive{physicalDriveIndex}",
            GENERIC_READ | GENERIC_WRITE,
            FileApi.FILE_SHARE_READ | FileApi.FILE_SHARE_WRITE,
            nint.Zero,
            FileApi.OPEN_EXISTING,
            0,
            nint.Zero);

        if (handle == Kernel32.INVALID_HANDLE_VALUE) {
            if (Marshal.GetLastPInvokeError() == ERROR_ACCESS_DENIED) {
                accessDenied = true;
            }

            return null;
        }

        try {
            int outSize = SendCmdOutParamsHeaderSize + SmartDataSize;

            byte* inBuffer = stackalloc byte[SendCmdInParamsHeaderSize];
            byte* outBuffer = stackalloc byte[outSize];
            new Span<byte>(inBuffer, SendCmdInParamsHeaderSize).Clear();
            new Span<byte>(outBuffer, outSize).Clear();

            // SENDCMDINPARAMS: cBufferSize(0), IDEREGS(4..11), bDriveNumber(12).
            *(uint*)inBuffer = SmartDataSize;
            inBuffer[4] = 0xD0;                                     // Features  = SMART READ DATA
            inBuffer[5] = 0x01;                                     // SectorCount
            inBuffer[6] = 0x01;                                     // SectorNumber
            inBuffer[7] = 0x4F;                                     // CylLow
            inBuffer[8] = 0xC2;                                     // CylHigh
            inBuffer[9] = (byte)(0xA0 | ((physicalDriveIndex & 1) << 4)); // DriveHead
            inBuffer[10] = 0xB0;                                    // Command   = SMART
            inBuffer[12] = (byte)physicalDriveIndex;

            uint returned = 0;

            bool ok = IoApiSet.DeviceIoControl(
                handle,
                SMART_RCV_DRIVE_DATA,
                inBuffer, SendCmdInParamsHeaderSize,
                outBuffer, (uint)outSize,
                &returned, nint.Zero);

            if (!ok || returned < SendCmdOutParamsHeaderSize + 2) {
                return null;
            }

            ReadOnlySpan<byte> attributeTable = new(
                outBuffer + SendCmdOutParamsHeaderSize,
                (int)Math.Min(returned - SendCmdOutParamsHeaderSize, (uint)SmartDataSize));

            return SmartAttributeTable.TemperatureCelsius(attributeTable);
        }
        finally {
            Kernel32.CloseHandle(handle);
        }
    }

    public void Dispose() { }
}

#pragma warning restore CA1416
#endif
