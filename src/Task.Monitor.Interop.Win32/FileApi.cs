using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class FileApi
{
    public const uint FILE_SHARE_READ  = 0x00000001;
    public const uint FILE_SHARE_WRITE = 0x00000002;
    public const uint OPEN_EXISTING    = 3;

    public const uint DRIVE_UNKNOWN     = 0;
    public const uint DRIVE_NO_ROOT_DIR = 1;
    public const uint DRIVE_REMOVABLE   = 2;
    public const uint DRIVE_FIXED       = 3;
    public const uint DRIVE_REMOTE      = 4;
    public const uint DRIVE_CDROM       = 5;
    public const uint DRIVE_RAMDISK     = 6;

    public const int ERROR_MORE_DATA     = 234;
    public const int ERROR_NO_MORE_FILES = 18;

    // Device paths are opened with dwDesiredAccess of zero. That is enough for the query IOCTLs
    // in WinIoCtl and, unlike a read handle, does not require an elevated token.
    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern nint CreateFileW(
        string lpFileName,
        uint   dwDesiredAccess,
        uint   dwShareMode,
        nint   lpSecurityAttributes,
        uint   dwCreationDisposition,
        uint   dwFlagsAndAttributes,
        nint   hTemplateFile);

    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern unsafe nint FindFirstVolumeW(
        char* lpszVolumeName,
        uint  cchBufferLength);

    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern unsafe bool FindNextVolumeW(
        nint  hFindVolume,
        char* lpszVolumeName,
        uint  cchBufferLength);

    [DllImport(Libraries.Kernel32, SetLastError = true)]
    public static extern bool FindVolumeClose(nint hFindVolume);

    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern unsafe bool GetVolumePathNamesForVolumeNameW(
        string lpszVolumeName,
        char*  lpszVolumePathNames,
        uint   cchBufferLength,
        uint*  lpcchReturnLength);

    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern unsafe bool GetVolumeInformationW(
        string lpRootPathName,
        char*  lpVolumeNameBuffer,
        uint   nVolumeNameSize,
        uint*  lpVolumeSerialNumber,
        uint*  lpMaximumComponentLength,
        uint*  lpFileSystemFlags,
        char*  lpFileSystemNameBuffer,
        uint   nFileSystemNameSize);

    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern unsafe bool GetDiskFreeSpaceExW(
        string lpDirectoryName,
        ulong* lpFreeBytesAvailableToCaller,
        ulong* lpTotalNumberOfBytes,
        ulong* lpTotalNumberOfFreeBytes);

    [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint GetDriveTypeW(string lpRootPathName);
}
