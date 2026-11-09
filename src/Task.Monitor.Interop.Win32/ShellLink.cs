using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

// ShellLink is bound through its raw vtable rather than through [ComImport] interfaces.
// taskmon publishes with PublishAot=true, and Native AOT has no built-in COM marshalling.
// We must call the vtable directly.
public static unsafe class ShellLink
{
    private static readonly Guid CLSID_ShellLink  = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid IID_IShellLinkW  = new("000214F9-0000-0000-C000-000000000046");
    private static readonly Guid IID_IPersistFile = new("0000010B-0000-0000-C000-000000000046");

    private const uint CLSCTX_INPROC_SERVER = 1;
    private const uint STGM_READ = 0;

    private const uint SLGP_RAWPATH = 0x4;

    private const int MAX_PATH = 260;
    private const int InfoTipSize = 1024;
    private const int Win32FindDataSize = 592;

    // IShellLinkW Vtable slots.
    private const int SlotGetPath = 3;
    private const int SlotGetArguments = 10;

    // IPersistFile Vtable slots.
    private const int SlotLoad = 5;

    public readonly record struct Target(string Path, string Arguments);

    private static string GetString(nint shellLink, int slot, int capacity, bool withFindData)
    {
        char* buffer = stackalloc char[capacity];
        new Span<char>(buffer, capacity).Clear();

        int hr;

        if (withFindData) {
            byte* findData = stackalloc byte[Win32FindDataSize];

            hr = ((delegate* unmanaged[Stdcall]<nint, char*, int, byte*, uint, int>)
                Ole32.Vtbl(shellLink)[slot])(shellLink, buffer, capacity, findData, SLGP_RAWPATH);
        }
        else {
            hr = ((delegate* unmanaged[Stdcall]<nint, char*, int, int>)
                Ole32.Vtbl(shellLink)[slot])(shellLink, buffer, capacity);
        }

        return hr == 0 
            ? new string(buffer) 
            : string.Empty;
    }

    public static Target? Resolve(string shortcutPath)
    {
        using ComInitScope com = ComInitScope.Initialise(Ole32.COINIT_APARTMENTTHREADED);

        int createHr = Ole32.CoCreateInstance(
            in CLSID_ShellLink,
            nint.Zero,
            CLSCTX_INPROC_SERVER,
            in IID_IShellLinkW,
            out nint shellLinkRef);

        using ComScope shellLink = new(shellLinkRef);

        if (createHr < 0 || shellLink.IsNull) {
            return null;
        }

        Guid persistFileIid = IID_IPersistFile;
        nint persistFileRef = nint.Zero;

        int queryHr = ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)
            Ole32.Vtbl(shellLink)[0])(shellLink, &persistFileIid, &persistFileRef);

        using ComScope persistFile = new(persistFileRef);

        if (queryHr < 0) {
            return null;
        }

        fixed (char* pFileName = shortcutPath) {
            int loadHr = ((delegate* unmanaged[Stdcall]<nint, char*, uint, int>)
                Ole32.Vtbl(persistFile)[SlotLoad])(persistFile, pFileName, STGM_READ);

            if (loadHr < 0) {
                return null;
            }
        }

        string path = GetString(
            shellLink,
            SlotGetPath,
            MAX_PATH,
            withFindData: true);

        if (path.Length == 0) {
            return null;
        }

        string arguments = GetString(
            shellLink,
            SlotGetArguments,
            InfoTipSize,
            withFindData: false);

        return new Target(path, arguments);
    }
}
