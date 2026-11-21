using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static unsafe class WinVer
{
    [StructLayout(LayoutKind.Sequential)]
    public struct VS_FIXEDFILEINFO
    {
        public uint dwSignature;
        public uint dwStrucVersion;
        public uint dwFileVersionMS;
        public uint dwFileVersionLS;
        public uint dwProductVersionMS;
        public uint dwProductVersionLS;
        public uint dwFileFlagsMask;
        public uint dwFileFlags;
        public uint dwFileOS;
        public uint dwFileType;
        public uint dwFileSubtype;
        public uint dwFileDateMS;
        public uint dwFileDateLS;
    }

    [DllImport(
        Libraries.Version,
        EntryPoint = "GetFileVersionInfoW",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetFileVersionInfo(
        string lptstrFilename,
        uint   dwHandle,
        uint   dwLen,
        byte*  lpData);

    [DllImport(
        Libraries.Version,
        EntryPoint = "GetFileVersionInfoSizeW",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    public static extern uint GetFileVersionInfoSize(string lptstrFilename, out uint lpdwHandle);

    [DllImport(
        Libraries.Version,
        EntryPoint = "VerQueryValueW",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool VerQueryValue(
        byte*     pBlock,
        string    lpSubBlock,
        out byte* lplpBuffer,
        out uint  puLen);
}
