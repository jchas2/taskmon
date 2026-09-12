namespace Task.Monitor.Interop.Win32;

/// <summary>
/// NVMe pass-through queries that run against the zero-access <c>\\.\PhysicalDriveN</c> handle
/// (no elevation): a log page, or an Identify structure. Built on
/// <see cref="IoApiSet.DeviceIoControl"/> with <see cref="WinIoCtl.IOCTL_STORAGE_QUERY_PROPERTY"/>
/// and a <c>STORAGE_PROTOCOL_SPECIFIC_DATA</c> request.
/// </summary>
public static unsafe class Nvme
{
    private const int HeaderSize = 8;            // PropertyId + QueryType
    private const int ProtocolDataSize = 40;     // STORAGE_PROTOCOL_SPECIFIC_DATA

    public static bool TryQueryLogPage(int physicalDriveIndex, uint logPageId, Span<byte> output) =>
        TryQuery(physicalDriveIndex, WinIoCtl.NVMeDataTypeLogPage, logPageId, output);

    public static bool TryQueryIdentify(int physicalDriveIndex, uint cns, Span<byte> output) =>
        TryQuery(physicalDriveIndex, WinIoCtl.NVMeDataTypeIdentify, cns, output);

    private static bool TryQuery(int physicalDriveIndex, uint dataType, uint requestValue, Span<byte> output)
    {
        if (output.Length == 0) {
            return false;
        }

        nint handle = FileApi.CreateFileW(
            $@"\\.\PhysicalDrive{physicalDriveIndex}",
            0,
            FileApi.FILE_SHARE_READ | FileApi.FILE_SHARE_WRITE,
            nint.Zero,
            FileApi.OPEN_EXISTING,
            0,
            nint.Zero);

        if (handle == Kernel32.INVALID_HANDLE_VALUE) {
            return false;
        }

        try {
            int total = HeaderSize + ProtocolDataSize + output.Length;
            byte[] buffer = new byte[total];

            fixed (byte* p = buffer) {
                *(uint*)p = WinIoCtl.StorageDeviceProtocolSpecificProperty;
                *(uint*)(p + 4) = WinIoCtl.PropertyStandardQuery;

                byte* proto = p + HeaderSize;
                *(uint*)(proto + WinIoCtl.ProtocolSpecificProtocolTypeOffset) = WinIoCtl.ProtocolTypeNvme;
                *(uint*)(proto + WinIoCtl.ProtocolSpecificDataTypeOffset) = dataType;
                *(uint*)(proto + WinIoCtl.ProtocolSpecificRequestValueOffset) = requestValue;
                *(uint*)(proto + WinIoCtl.ProtocolSpecificDataOffsetOffset) = ProtocolDataSize;
                *(uint*)(proto + WinIoCtl.ProtocolSpecificDataLengthOffset) = (uint)output.Length;

                uint returned = 0;

                bool ok = IoApiSet.DeviceIoControl(
                    handle,
                    WinIoCtl.IOCTL_STORAGE_QUERY_PROPERTY,
                    p, (uint)total,
                    p, (uint)total,
                    &returned, nint.Zero);

                if (!ok || returned < HeaderSize + ProtocolDataSize) {
                    return false;
                }

                byte* responseProto = p + HeaderSize;
                uint dataOffset = *(uint*)(responseProto + WinIoCtl.ProtocolSpecificDataOffsetOffset);
                uint dataLength = *(uint*)(responseProto + WinIoCtl.ProtocolSpecificDataLengthOffset);

                int start = HeaderSize + (int)dataOffset;

                // Some drivers leave the response descriptor blank; fall back to the standard place.
                if (dataLength < (uint)output.Length || start < 0 || start + output.Length > total) {
                    start = HeaderSize + ProtocolDataSize;
                }

                if (start + output.Length > total) {
                    return false;
                }

                new ReadOnlySpan<byte>(p + start, output.Length).CopyTo(output);
                return true;
            }
        }
        finally {
            Kernel32.CloseHandle(handle);
        }
    }
}
