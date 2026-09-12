using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

// Minimal binding of version.dll, used to read a file's version resource - who published it
// (CompanyName) and its version - without pulling in a signature check.
public static unsafe class WinVer
{
    [DllImport(Libraries.Version, EntryPoint = "GetFileVersionInfoSizeW", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint GetFileVersionInfoSize(string lptstrFilename, out uint lpdwHandle);

    [DllImport(Libraries.Version, EntryPoint = "GetFileVersionInfoW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetFileVersionInfo(string lptstrFilename, uint dwHandle, uint dwLen, byte* lpData);

    [DllImport(Libraries.Version, EntryPoint = "VerQueryValueW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool VerQueryValue(byte* pBlock, string lpSubBlock, out byte* lplpBuffer, out uint puLen);

    // Returns the CompanyName recorded in the file's version resource, or null when the file has no
    // version resource or no such string (common for scripts, self-built tools and many drivers).
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
            if (!GetFileVersionInfo(filePath, 0, size, pBlock)) {
                return null;
            }

            // \VarFileInfo\Translation is an array of {langId, codePage} ushort pairs. Try each in
            // turn, then the two ubiquitous fallbacks, so a file whose resource is tagged with an
            // unexpected locale still resolves.
            foreach (string translation in EnumerateTranslations(pBlock)) {
                string subBlock = $@"\StringFileInfo\{translation}\CompanyName";

                if (VerQueryValue(pBlock, subBlock, out byte* pValue, out uint valueLength) &&
                    valueLength > 1) {

                    string value = new string((char*)pValue, 0, (int)valueLength - 1).Trim();

                    if (value.Length > 0) {
                        return value;
                    }
                }
            }
        }

        return null;
    }

    // The fixed-info block's file-version fields, laid out MS/LS-word-pairs rather than four
    // separate fields - see winver.h's VS_FIXEDFILEINFO. Reading these instead of the
    // \StringFileInfo\{translation}\FileVersion string sidesteps that string being locale-tagged,
    // sometimes absent, and free-text (vendors format it inconsistently); the numeric fields are
    // always present whenever the resource itself is, and match what Explorer's Details tab and
    // driverquery /v both show.
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

    // Returns "major.minor.build.revision" from the file's version resource, or null when the file
    // has none (common for third-party or very old drivers).
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

            if (!VerQueryValue(pBlock, @"\", out byte* pValue, out uint valueLength) ||
                valueLength < sizeof(VS_FIXEDFILEINFO)) {
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

        if (VerQueryValue(pBlock, @"\VarFileInfo\Translation", out byte* pTranslations, out uint length)) {
            for (uint offset = 0; offset + 4 <= length; offset += 4) {
                ushort langId = *(ushort*)(pTranslations + offset);
                ushort codePage = *(ushort*)(pTranslations + offset + 2);
                translations.Add($"{langId:x4}{codePage:x4}");
            }
        }

        translations.Add("040904b0"); // US English, Unicode
        translations.Add("040904e4"); // US English, Windows Multilingual

        return translations;
    }
}
