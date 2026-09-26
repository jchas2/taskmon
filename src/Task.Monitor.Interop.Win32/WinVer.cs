using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static unsafe class WinVer
{
    [DllImport(
        Libraries.Version, 
        EntryPoint = "GetFileVersionInfoSizeW", 
        SetLastError = true, 
        CharSet = CharSet.Unicode)]
    public static extern uint GetFileVersionInfoSize(string lptstrFilename, out uint lpdwHandle);

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
        EntryPoint = "VerQueryValueW", 
        SetLastError = true, 
        CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool VerQueryValue(
        byte*     pBlock, 
        string    lpSubBlock, 
        out byte* lplpBuffer, 
        out uint  puLen);

    [StructLayout(LayoutKind.Sequential)]
    private struct VS_FIXEDFILEINFO
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

    public static string? GetCompanyName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
            return null;
        }

        uint size = GetFileVersionInfoSize(filePath, out _);

        if (size == 0) {
            return null;
        }

        byte[] block = new byte[size];

        fixed (byte* pBlock = block) {
            if (!GetFileVersionInfo(
                filePath, 
                0, 
                size, 
                pBlock)) {
                
                return null;
            }

            foreach (string translation in EnumerateTranslations(pBlock)) {
                string subBlock = $@"\StringFileInfo\{translation}\CompanyName";

                if (VerQueryValue(
                    pBlock, 
                    subBlock, 
                    out byte* pValue, 
                    out uint valueLength) && valueLength > 1) {

                    string value = new string((char*)pValue, 0, (int)valueLength - 1).Trim();

                    if (value.Length > 0) {
                        return value;
                    }
                }
            }
        }

        return null;
    }

    public static string? GetFileVersion(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
            return null;
        }

        uint size = GetFileVersionInfoSize(filePath, out _);

        if (size == 0) {
            return null;
        }

        byte[] block = new byte[size];

        fixed (byte* pBlock = block) {
            if (!GetFileVersionInfo(filePath, 0, size, pBlock)) {
                return null;
            }

            if (!VerQueryValue(
                pBlock, 
                @"\", 
                out byte* pValue, 
                out uint valueLength) || valueLength < sizeof(VS_FIXEDFILEINFO)) {
                
                return null;
            }

            VS_FIXEDFILEINFO info = *(VS_FIXEDFILEINFO*)pValue;

            return $"{HighWord(info.dwFileVersionMS)}.{LowWord(info.dwFileVersionMS)}." +
                   $"{HighWord(info.dwFileVersionLS)}.{LowWord(info.dwFileVersionLS)}";
        }
    }

    private static uint HighWord(uint value) => value >> 16;
    private static uint LowWord(uint value) => value & 0xFFFF;

    private static List<string> EnumerateTranslations(byte* pBlock)
    {
        List<string> translations = new();

        if (VerQueryValue(
            pBlock, 
            @"\VarFileInfo\Translation", 
            out byte* pTranslations, 
            out uint length)) {
            
            for (uint offset = 0; offset + 4 <= length; offset += 4) {
                ushort langId = *(ushort*)(pTranslations + offset);
                ushort codePage = *(ushort*)(pTranslations + offset + 2);
                translations.Add($"{langId:x4}{codePage:x4}");
            }
        }

        translations.Add("040904b0"); // US English, Unicode.
        translations.Add("040904e4"); // US English, Windows Multilingual.

        return translations;
    }
}
