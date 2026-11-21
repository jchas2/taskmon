#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Native;

public static unsafe class VersionResource
{
    private static List<string> EnumerateTranslations(byte* pBlock)
    {
        List<string> translations = new();

        if (WinVer.VerQueryValue(
            pBlock,
            @"\VarFileInfo\Translation",
            out byte* pTranslations,
            out uint length)) {

            for (uint offset = 0; offset + 4 <= length; offset += 4) {
                ushort langId   = *(ushort*)(pTranslations + offset);
                ushort codePage = *(ushort*)(pTranslations + offset + 2);
                translations.Add($"{langId:x4}{codePage:x4}");
            }
        }

        // Files without a usable translation table usually carry one of these.
        translations.Add("040904b0"); // US English, Unicode.
        translations.Add("040904e4"); // US English, Windows Multilingual.

        return translations;
    }

    public static string? GetCompanyName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
            return null;
        }

        uint size = WinVer.GetFileVersionInfoSize(filePath, out _);

        if (size == 0) {
            return null;
        }

        byte[] block = new byte[size];

        fixed (byte* pBlock = block) {
            if (!WinVer.GetFileVersionInfo(filePath, 0, size, pBlock)) {
                return null;
            }

            foreach (string translation in EnumerateTranslations(pBlock)) {
                string subBlock = $@"\StringFileInfo\{translation}\CompanyName";

                if (WinVer.VerQueryValue(
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

        uint size = WinVer.GetFileVersionInfoSize(filePath, out _);

        if (size == 0) {
            return null;
        }

        byte[] block = new byte[size];

        fixed (byte* pBlock = block) {
            if (!WinVer.GetFileVersionInfo(filePath, 0, size, pBlock)) {
                return null;
            }

            if (!WinVer.VerQueryValue(
                pBlock,
                @"\",
                out byte* pValue,
                out uint valueLength) || valueLength < sizeof(WinVer.VS_FIXEDFILEINFO)) {

                return null;
            }

            WinVer.VS_FIXEDFILEINFO info = *(WinVer.VS_FIXEDFILEINFO*)pValue;

            return $"{HighWord(info.dwFileVersionMS)}.{LowWord(info.dwFileVersionMS)}." +
                   $"{HighWord(info.dwFileVersionLS)}.{LowWord(info.dwFileVersionLS)}";
        }
    }

    private static uint HighWord(uint value) => value >> 16;

    private static uint LowWord(uint value) => value & 0xFFFF;
}
#endif
