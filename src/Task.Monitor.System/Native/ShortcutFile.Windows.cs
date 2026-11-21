#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Native;

public static unsafe class ShortcutFile
{
    public readonly record struct Target(string Path, string Arguments);

    private static string GetArguments(nint shellLink)
    {
        char* buffer = stackalloc char[ShellLink.INFOTIPSIZE];
        new Span<char>(buffer, ShellLink.INFOTIPSIZE).Clear();

        return ShellLink.IShellLinkW_GetArguments(shellLink, buffer, ShellLink.INFOTIPSIZE) == 0
            ? new string(buffer)
            : string.Empty;
    }

    private static string GetPath(nint shellLink, out int hr)
    {
        char* buffer   = stackalloc char[Kernel32.MAX_PATH];
        byte* findData = stackalloc byte[ShellLink.Win32FindDataSize];
        new Span<char>(buffer, Kernel32.MAX_PATH).Clear();

        hr = ShellLink.IShellLinkW_GetPath(shellLink, buffer, Kernel32.MAX_PATH, findData, ShellLink.SLGP_RAWPATH);

        return hr == 0
            ? new string(buffer)
            : string.Empty;
    }

    public static Target? Resolve(string shortcutPath, out ComFailure? failure)
    {
        failure = null;

        using ComInitScope com = ComInitScope.Initialise(Ole32.COINIT_APARTMENTTHREADED);

        int createHr = Ole32.CoCreateInstance(
            in ShellLink.CLSID_ShellLink,
            nint.Zero,
            Ole32.CLSCTX_INPROC_SERVER,
            in ShellLink.IID_IShellLinkW,
            out nint shellLinkRef);

        using ComScope shellLink = new(shellLinkRef);

        if (createHr < 0 || shellLink.IsNull) {
            failure = new ComFailure($"{nameof(Ole32.CoCreateInstance)} ShellLink", createHr);
            return null;
        }

        Guid persistFileIid = ShellLink.IID_IPersistFile;
        nint persistFileRef = nint.Zero;

        int queryHr = Ole32.IUnknown_QueryInterface(shellLink, &persistFileIid, &persistFileRef);

        using ComScope persistFile = new(persistFileRef);

        if (queryHr < 0) {
            failure = new ComFailure("IUnknown::QueryInterface IPersistFile", queryHr);
            return null;
        }

        fixed (char* pFileName = shortcutPath) {
            int loadHr = ShellLink.IPersistFile_Load(persistFile, pFileName, ShellLink.STGM_READ);

            if (loadHr < 0) {
                failure = new ComFailure("IPersistFile::Load", loadHr);
                return null;
            }
        }

        string path = GetPath(shellLink, out int pathHr);

        if (path.Length == 0) {
            failure = new ComFailure("IShellLinkW::GetPath", pathHr);
            return null;
        }

        return new Target(path, GetArguments(shellLink));
    }
}
#endif
