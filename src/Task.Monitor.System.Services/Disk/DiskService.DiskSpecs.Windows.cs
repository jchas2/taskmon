using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Disk;

public partial class DiskService
{
#if __WIN32__
    // \\.\PhysicalDriveN numbering can have gaps, so the scan runs the whole range and skips the
    // indices that fail to open rather than stopping at the first one.
    private const int MaxPhysicalDrives = 64;

    private const uint DeviceQueryAccess = 0;
    private const uint DeviceShareMode = FileApi.FILE_SHARE_READ | FileApi.FILE_SHARE_WRITE;

    private unsafe void OnStartDiskSpecs(DiskSpecs specs)
    {
        uint previousErrorMode = 0;
        bool errorModeChanged = ErrHandlingApi.SetThreadErrorMode(
            ErrHandlingApi.SEM_FAILCRITICALERRORS,
            &previousErrorMode);

        try {
            EnumeratePhysicalDrives(specs);
            EnumerateVolumes(specs);
        }
        finally {
            if (errorModeChanged) {
                ErrHandlingApi.SetThreadErrorMode(previousErrorMode, null);
            }
        }
    }

    private static void EnumeratePhysicalDrives(DiskSpecs specs)
    {
        for (int index = 0; index < MaxPhysicalDrives; index++)
        {
            string devicePath = $@"\\.\PhysicalDrive{index}";

            nint handle = FileApi.CreateFileW(
                devicePath,
                DeviceQueryAccess,
                DeviceShareMode,
                nint.Zero,
                FileApi.OPEN_EXISTING,
                0,
                nint.Zero);

            if (handle == Kernel32.INVALID_HANDLE_VALUE) {
                continue;
            }

            try {
                byte[]? descriptor = QueryDeviceDescriptor(handle, devicePath);

                DiskDevice device = descriptor != null
                    ? DiskDeviceParser.Parse(descriptor)
                    : new DiskDevice();

                device.Index = index;
                device.Capacity = QueryCapacity(handle, devicePath);
                device.MediaType = ResolveMediaType(handle, device);

                specs.Devices.Add(device);
            }
            finally {
                Kernel32.CloseHandle(handle);
            }
        }
    }

    private static unsafe byte[]? QueryDeviceDescriptor(nint handle, string devicePath)
    {
        WinIoCtl.STORAGE_PROPERTY_QUERY query = new() {
            PropertyId = WinIoCtl.StorageDeviceProperty,
            QueryType  = WinIoCtl.PropertyStandardQuery,
        };

        WinIoCtl.STORAGE_DESCRIPTOR_HEADER header = new();
        uint returned = 0;

        // The descriptor is variable length, so the header is fetched first purely for its Size.
        if (!IoApiSet.DeviceIoControl(
            handle,
            WinIoCtl.IOCTL_STORAGE_QUERY_PROPERTY,
            &query,
            (uint)sizeof(WinIoCtl.STORAGE_PROPERTY_QUERY),
            &header,
            (uint)sizeof(WinIoCtl.STORAGE_DESCRIPTOR_HEADER),
            &returned,
            nint.Zero)) {

            PInvokeErrorHelpers.TraceOnceOnLastError(
                $"{nameof(WinIoCtl.IOCTL_STORAGE_QUERY_PROPERTY)} {devicePath}",
                $"Failed {nameof(QueryDeviceDescriptor)} header for {devicePath}");

            return null;
        }

        if (header.Size < WinIoCtl.StorageDeviceDescriptorMinimumLength) {
            TraceEx.WriteLineOnce(
                $"{nameof(QueryDeviceDescriptor)} {devicePath}",
                $"STORAGE_DEVICE_DESCRIPTOR for {devicePath} is {header.Size} bytes, " +
                $"expected at least {WinIoCtl.StorageDeviceDescriptorMinimumLength}");

            return null;
        }

        byte[] descriptor = new byte[header.Size];

        fixed (byte* buffer = descriptor) {
            if (!IoApiSet.DeviceIoControl(
                handle,
                WinIoCtl.IOCTL_STORAGE_QUERY_PROPERTY,
                &query,
                (uint)sizeof(WinIoCtl.STORAGE_PROPERTY_QUERY),
                buffer,
                header.Size,
                &returned,
                nint.Zero)) {

                PInvokeErrorHelpers.TraceOnceOnLastError(
                    $"{nameof(WinIoCtl.IOCTL_STORAGE_QUERY_PROPERTY)} descriptor {devicePath}",
                    $"Failed {nameof(QueryDeviceDescriptor)} for {devicePath}");

                return null;
            }
        }

        return descriptor;
    }

    private static string ResolveMediaType(nint handle, DiskDevice device)
    {
        bool hasSeekPenalty = TryQuerySeekPenalty(handle, out bool incursSeekPenalty);
        bool hasTrim = TryQueryTrim(handle, out bool trimEnabled);

        return DiskDeviceParser.DecodeMediaType(
            device.BusType,
            device.IsRemovable,
            hasSeekPenalty,
            incursSeekPenalty,
            hasTrim,
            trimEnabled);
    }

    // Both of these are optional properties that plenty of controllers decline to answer,
    // notably USB bridges. A failure is an expected answer of "do not know" rather than an
    // error worth tracing once per device.
    private static unsafe bool TryQuerySeekPenalty(nint handle, out bool incursSeekPenalty)
    {
        incursSeekPenalty = false;

        WinIoCtl.STORAGE_PROPERTY_QUERY query = new() {
            PropertyId = WinIoCtl.StorageDeviceSeekPenaltyProperty,
            QueryType  = WinIoCtl.PropertyStandardQuery,
        };

        WinIoCtl.DEVICE_SEEK_PENALTY_DESCRIPTOR descriptor = new();
        uint returned = 0;

        if (!IoApiSet.DeviceIoControl(
            handle,
            WinIoCtl.IOCTL_STORAGE_QUERY_PROPERTY,
            &query,
            (uint)sizeof(WinIoCtl.STORAGE_PROPERTY_QUERY),
            &descriptor,
            (uint)sizeof(WinIoCtl.DEVICE_SEEK_PENALTY_DESCRIPTOR),
            &returned,
            nint.Zero)) {

            return false;
        }

        incursSeekPenalty = descriptor.IncursSeekPenalty != 0;
        return true;
    }

    private static unsafe bool TryQueryTrim(nint handle, out bool trimEnabled)
    {
        trimEnabled = false;

        WinIoCtl.STORAGE_PROPERTY_QUERY query = new() {
            PropertyId = WinIoCtl.StorageDeviceTrimProperty,
            QueryType  = WinIoCtl.PropertyStandardQuery,
        };

        WinIoCtl.DEVICE_TRIM_DESCRIPTOR descriptor = new();
        uint returned = 0;

        if (!IoApiSet.DeviceIoControl(
            handle,
            WinIoCtl.IOCTL_STORAGE_QUERY_PROPERTY,
            &query,
            (uint)sizeof(WinIoCtl.STORAGE_PROPERTY_QUERY),
            &descriptor,
            (uint)sizeof(WinIoCtl.DEVICE_TRIM_DESCRIPTOR),
            &returned,
            nint.Zero)) {

            return false;
        }

        trimEnabled = descriptor.TrimEnabled != 0;
        return true;
    }

    // IOCTL_DISK_GET_DRIVE_GEOMETRY_EX rather than IOCTL_DISK_GET_LENGTH_INFO: the latter needs a
    // read handle and therefore an elevated token, the former runs on the query handle above.
    private static unsafe long QueryCapacity(nint handle, string devicePath)
    {
        byte* buffer = stackalloc byte[WinIoCtl.DiskGeometryExBufferSize];
        new Span<byte>(buffer, WinIoCtl.DiskGeometryExBufferSize).Clear();

        uint returned = 0;

        if (!IoApiSet.DeviceIoControl(
            handle,
            WinIoCtl.IOCTL_DISK_GET_DRIVE_GEOMETRY_EX,
            null,
            0,
            buffer,
            WinIoCtl.DiskGeometryExBufferSize,
            &returned,
            nint.Zero)) {

            PInvokeErrorHelpers.TraceOnceOnLastError(
                $"{nameof(WinIoCtl.IOCTL_DISK_GET_DRIVE_GEOMETRY_EX)} {devicePath}",
                $"Failed {nameof(QueryCapacity)} for {devicePath}");

            return 0;
        }

        if (returned < WinIoCtl.DiskGeometryExDiskSizeOffset + sizeof(long)) {
            return 0;
        }

        return *(long*)(buffer + WinIoCtl.DiskGeometryExDiskSizeOffset);
    }

    private static unsafe void EnumerateVolumes(DiskSpecs specs)
    {
        char* volumeNameBuffer = stackalloc char[Kernel32.MAX_PATH];
        nint findHandle = FileApi.FindFirstVolumeW(volumeNameBuffer, Kernel32.MAX_PATH);

        if (findHandle == Kernel32.INVALID_HANDLE_VALUE) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                nameof(FileApi.FindFirstVolumeW),
                $"Failed {nameof(EnumerateVolumes)}");

            return;
        }

        try {
            do {
                AddVolume(specs, new string(volumeNameBuffer));
            }
            while (FileApi.FindNextVolumeW(findHandle, volumeNameBuffer, Kernel32.MAX_PATH));

            int error = Marshal.GetLastPInvokeError();

            if (error != FileApi.ERROR_NO_MORE_FILES) {
                PInvokeErrorHelpers.TraceOnceOnLastError(
                    nameof(FileApi.FindNextVolumeW),
                    $"Failed {nameof(EnumerateVolumes)}");
            }
        }
        finally {
            FileApi.FindVolumeClose(findHandle);
        }
    }

    private static void AddVolume(DiskSpecs specs, string volumeName)
    {
        DiskVolume volume = new();
        volume.VolumeName = volumeName;
        volume.MountPoints = QueryMountPoints(volumeName);
        volume.DriveType = DiskDeviceParser.DecodeDriveType(FileApi.GetDriveTypeW(volumeName));
        volume.IsReady = TryQueryVolumeInformation(volumeName, volume);

        QueryVolumeCapacity(volumeName, volume);
        AttachVolume(specs, volumeName, volume);
    }

    private static unsafe string[] QueryMountPoints(string volumeName)
    {
        char* names = stackalloc char[Kernel32.MAX_PATH];
        uint length = Kernel32.MAX_PATH;

        if (FileApi.GetVolumePathNamesForVolumeNameW(volumeName, names, (uint)Kernel32.MAX_PATH, &length)) {
            return MultiSzParser.Parse(
                new ReadOnlySpan<char>(names, (int)Math.Min(length, Kernel32.MAX_PATH)));
        }

        if (Marshal.GetLastPInvokeError() != FileApi.ERROR_MORE_DATA) {
            PInvokeErrorHelpers.TraceOnceOnLastError(
                $"{nameof(FileApi.GetVolumePathNamesForVolumeNameW)} {volumeName}",
                $"Failed {nameof(QueryMountPoints)} for {volumeName}");

            return [];
        }

        // A volume mounted at many paths at once needs more room than MAX_PATH.
        if (length == 0) {
            return [];
        }

        char[] buffer = new char[length];

        fixed (char* heapNames = buffer) {
            if (!FileApi.GetVolumePathNamesForVolumeNameW(volumeName, heapNames, length, &length)) {
                PInvokeErrorHelpers.TraceOnceOnLastError(
                    $"{nameof(FileApi.GetVolumePathNamesForVolumeNameW)} grown {volumeName}",
                    $"Failed {nameof(QueryMountPoints)} for {volumeName}");

                return [];
            }
        }

        return MultiSzParser.Parse(buffer.AsSpan(0, (int)Math.Min(length, (uint)buffer.Length)));
    }

    private static unsafe bool TryQueryVolumeInformation(string volumeName, DiskVolume volume)
    {
        char* label = stackalloc char[Kernel32.MAX_PATH];
        char* fileSystem = stackalloc char[Kernel32.MAX_PATH];

        uint serialNumber = 0;
        uint maximumComponentLength = 0;
        uint fileSystemFlags = 0;

        // Fails for an empty card reader, an unformatted RAW volume or a disconnected network
        // mount. The volume is still worth keeping, just without its filesystem detail.
        if (!FileApi.GetVolumeInformationW(
            volumeName,
            label,
            Kernel32.MAX_PATH,
            &serialNumber,
            &maximumComponentLength,
            &fileSystemFlags,
            fileSystem,
            Kernel32.MAX_PATH)) {

            return false;
        }

        volume.Label = new string(label);
        volume.FileSystem = new string(fileSystem);
        volume.SerialNumber = serialNumber;

        return true;
    }

    private static unsafe void QueryVolumeCapacity(string volumeName, DiskVolume volume)
    {
        ulong freeBytesAvailable = 0;
        ulong totalBytes = 0;
        ulong totalFreeBytes = 0;

        if (!FileApi.GetDiskFreeSpaceExW(
            volumeName,
            &freeBytesAvailable,
            &totalBytes,
            &totalFreeBytes)) {

            return;
        }

        volume.FormattedCapacity = (long)totalBytes;
        volume.AvailableFreeSpace = (long)freeBytesAvailable;
        volume.UsedRatio = totalBytes > 0
            ? 1.0 - (freeBytesAvailable / (double)totalBytes)
            : 0.0;
    }

    private static void AttachVolume(DiskSpecs specs, string volumeName, DiskVolume volume)
    {
        // The query APIs above require the trailing separator on a volume GUID path. CreateFileW
        // rejects it. Same string, two forms.
        string devicePath = volumeName.TrimEnd('\\');

        nint handle = FileApi.CreateFileW(
            devicePath,
            DeviceQueryAccess,
            DeviceShareMode,
            nint.Zero,
            FileApi.OPEN_EXISTING,
            0,
            nint.Zero);

        if (handle == Kernel32.INVALID_HANDLE_VALUE) {
            specs.UnattachedVolumes.Add(volume);
            return;
        }

        try {
            if (!TryQueryDiskExtents(handle, out uint[] diskNumbers)) {
                specs.UnattachedVolumes.Add(volume);
                return;
            }

            bool attached = false;

            // A spanned or striped volume genuinely lives on several disks, so it is attached to
            // each of them rather than arbitrarily to the first.
            foreach (uint diskNumber in diskNumbers) {
                DiskDevice? device = specs.Devices.FirstOrDefault(disk => disk.Index == (int)diskNumber);

                if (device == null) {
                    continue;
                }

                device.Volumes.Add(volume);
                attached = true;
            }

            if (!attached) {
                specs.UnattachedVolumes.Add(volume);
            }
        }
        finally {
            Kernel32.CloseHandle(handle);
        }
    }

    private static unsafe bool TryQueryDiskExtents(nint handle, out uint[] diskNumbers)
    {
        diskNumbers = [];

        int size = WinIoCtl.VolumeDiskExtentsArrayOffset + WinIoCtl.DiskExtentSize;

        // The first call reports how many extents there really are, so a second sized call can
        // collect them all.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            byte[] buffer = new byte[size];
            uint returned = 0;
            bool succeeded;

            fixed (byte* extents = buffer) {
                succeeded = IoApiSet.DeviceIoControl(
                    handle,
                    WinIoCtl.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
                    null,
                    0,
                    extents,
                    (uint)size,
                    &returned,
                    nint.Zero);
            }

            int error = Marshal.GetLastPInvokeError();
            uint extentCount = BinaryPrimitives.ReadUInt32LittleEndian(
                buffer.AsSpan(WinIoCtl.VolumeDiskExtentsCountOffset));

            if (!succeeded) {
                if (error != FileApi.ERROR_MORE_DATA || extentCount == 0 || attempt == 1) {
                    return false;
                }

                size = WinIoCtl.VolumeDiskExtentsArrayOffset +
                       ((int)extentCount * WinIoCtl.DiskExtentSize);

                continue;
            }

            if (extentCount == 0) {
                return false;
            }

            int capacity = (buffer.Length - WinIoCtl.VolumeDiskExtentsArrayOffset) / WinIoCtl.DiskExtentSize;
            int usable = (int)Math.Min(extentCount, (uint)capacity);
            uint[] numbers = new uint[usable];

            for (int extent = 0; extent < usable; extent++) {
                int offset = WinIoCtl.VolumeDiskExtentsArrayOffset +
                             (extent * WinIoCtl.DiskExtentSize) +
                             WinIoCtl.DiskExtentDiskNumberOffset;

                numbers[extent] = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset));
            }

            diskNumbers = numbers;
            return true;
        }

        return false;
    }
#endif
}
