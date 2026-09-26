using System.Diagnostics;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;

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

    public static Target? Resolve(string shortcutPath)
    {
        Target? target = null;
        nint shellLink = nint.Zero;
        nint persistFile = nint.Zero;

        int hResult = Ole32.ComInitializeEx(Ole32.COINIT_APARTMENTTHREADED);

        if (Ole32.CoCreateInstance(
            in CLSID_ShellLink,
            nint.Zero,
            CLSCTX_INPROC_SERVER,
            in IID_IShellLinkW,
            out shellLink) < 0 || shellLink == nint.Zero) {

            goto Finished;
        }

        Guid persistFileIid = IID_IPersistFile;

        int queryHr = ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)
            Ole32.Vtbl(shellLink)[0])(shellLink, &persistFileIid, &persistFile);

        if (queryHr < 0) {
            goto Finished;
        }

        fixed (char* pFileName = shortcutPath) {
            int loadHr = ((delegate* unmanaged[Stdcall]<nint, char*, uint, int>)
                Ole32.Vtbl(persistFile)[SlotLoad])(persistFile, pFileName, STGM_READ);

            if (loadHr < 0) {
                goto Finished;
            }
        }

        string path = GetString(
            shellLink, 
            SlotGetPath, 
            MAX_PATH, 
            withFindData: true);

        if (path.Length != 0) {
            string arguments = GetString(
                shellLink, 
                SlotGetArguments, 
                InfoTipSize, 
                withFindData: false);
            
            target = new Target(path, arguments);
        }
        
        Finished: 
        InteropHelper.MarshalRelease(ref persistFile);
        InteropHelper.MarshalRelease(ref shellLink);
        Ole32.ComUninitialize(hResult);
        return target;
    }

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
}
