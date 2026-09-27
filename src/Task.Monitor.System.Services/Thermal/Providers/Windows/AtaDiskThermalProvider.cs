#if __WIN32__
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Thermal.Providers.Windows;

// SATA / ATA drives.
internal sealed unsafe class AtaDiskThermalProvider(Func<IReadOnlyList<DiskDevice>> getDisks) : IThermalProvider
{
    private const uint SMART_RCV_DRIVE_DATA = 0x0007C088;
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const int  ERROR_ACCESS_DENIED = 5;

    private const int SendCmdInParamsHeaderSize = 32;
    private const int SendCmdOutParamsHeaderSize = 16;
    private const int SmartDataSize = 512;

    private bool accessDenied;

    public string Name => "ATA SMART";

    public bool TryInitialise() => true;

    public IEnumerable<ThermalSensor> Read()
    {
        // Always abandon if not running with the required elevated privileges.
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
                    Component   = ThermalComponent.Disk,
                    ComponentId = disk.Index.ToString(),
                    SensorName  = "Drive",
                    Celsius     = value,
                    Source      = ThermalSource.AtaSmart
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

            // The following inBuffer values must be populated when using SMART_RCV_DRIVE_DATA
            // with DeviceIoControl.
            *(uint*)inBuffer = SmartDataSize;
            inBuffer[4] = 0xD0;                                           // Features  = SMART READ DATA
            inBuffer[5] = 0x01;                                           // SectorCount
            inBuffer[6] = 0x01;                                           // SectorNumber
            inBuffer[7] = 0x4F;                                           // CylLow
            inBuffer[8] = 0xC2;                                           // CylHigh
            inBuffer[9] = (byte)(0xA0 | ((physicalDriveIndex & 1) << 4)); // DriveHead
            inBuffer[10] = 0xB0;                                          // Command   = SMART
            inBuffer[12] = (byte)physicalDriveIndex;

            uint returned = 0;

            bool ok = IoApiSet.DeviceIoControl(
                handle,
                SMART_RCV_DRIVE_DATA,
                inBuffer, 
                SendCmdInParamsHeaderSize,
                outBuffer, 
                (uint)outSize,
                &returned, 
                nint.Zero);

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
#endif
