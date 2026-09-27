#if __WIN32__
using System.Buffers.Binary;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Thermal.Providers.Windows;

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
                    Component   = ThermalComponent.Disk,
                    ComponentId = id,
                    SensorName  = "Composite",
                    Celsius     = composite,
                    Source      = ThermalSource.NvmeHealthLog
                };
            }

            foreach ((int index, double celsius) in NvmeHealthLog.Sensors(log)) {
                yield return new ThermalSensor {
                    Component   = ThermalComponent.Disk,
                    ComponentId = id,
                    SensorName  = $"Sensor {index}",
                    Celsius     = celsius,
                    Source      = ThermalSource.NvmeHealthLog
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
            *(uint*)(proto + WinIoCtl.ProtocolSpecificDataTypeOffset)     = WinIoCtl.NVMeDataTypeLogPage;
            *(uint*)(proto + WinIoCtl.ProtocolSpecificRequestValueOffset) = WinIoCtl.NVMeLogPageHealthInfo;
            *(uint*)(proto + WinIoCtl.ProtocolSpecificDataOffsetOffset)   = ProtocolDataSize;
            *(uint*)(proto + WinIoCtl.ProtocolSpecificDataLengthOffset)   = LogSize;

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

            // Some drivers do not fill the response descriptor, fall back to the standard segment.
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
#endif
