using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

// Minimal binding of version.dll, used to read the CompanyName string from a file's version
// resource. Enough to answer "who published this" for a startup entry without pulling in a
// signature check.
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
