using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

/// <summary>
/// Resolves a Windows shell shortcut (.lnk) to its target path and arguments.
///
/// Bound through the raw COM vtable rather than [ComImport] interfaces, for the same reason as
/// <see cref="Dxgi"/>: taskmon publishes with PublishAot=true and Native AOT has no built-in COM
/// marshalling, so an RCW based binding compiles but fails at runtime in the published build.
/// CoCreateInstance hands back an IUnknown pointer; QueryInterface / Release on it are raw pointer
/// operations and stay valid either way.
/// </summary>
public static unsafe class ShellLink
{
    private static readonly Guid CLSID_ShellLink = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid IID_IShellLinkW = new("000214F9-0000-0000-C000-000000000046");
    private static readonly Guid IID_IPersistFile = new("0000010B-0000-0000-C000-000000000046");

    private const uint CLSCTX_INPROC_SERVER = 1;
    private const uint STGM_READ = 0;

    // IShellLinkW::GetPath fFlags - return the target path exactly as stored, skipping the short
    // (8.3) form and the environment-variable / install-source rewriting the default applies.
    private const uint SLGP_RAWPATH = 0x4;

    private const int MAX_PATH = 260;
    private const int InfoTipSize = 1024;
    private const int Win32FindDataSize = 592;

    // Vtable slots past IUnknown (QueryInterface 0, AddRef 1, Release 2).
    //   IShellLinkW  GetPath 3, GetIDList 4, SetIDList 5, GetDescription 6, SetDescription 7,
    //                GetWorkingDirectory 8, SetWorkingDirectory 9, GetArguments 10, SetArguments 11
    private const int SlotGetPath = 3;
    private const int SlotGetArguments = 10;

    //   IPersistFile  GetClassID 3, IsDirty 4, Load 5
    private const int SlotLoad = 5;

    [DllImport(Libraries.Ole32, PreserveSig = true)]
    private static extern int CoInitializeEx(nint pvReserved, uint dwCoInit);

    [DllImport(Libraries.Ole32)]
    private static extern void CoUninitialize();

    [DllImport(Libraries.Ole32, PreserveSig = true)]
    private static extern int CoCreateInstance(
        in Guid rclsid,
        nint pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out nint ppv);

    public readonly record struct Target(string Path, string Arguments);

    /// <summary>
    /// Resolves <paramref name="shortcutPath"/>. Returns null when the file is not a shortcut, the
    /// shell cannot load it, or it has no file-system target (it points at a virtual folder).
    /// </summary>
    public static Target? Resolve(string shortcutPath)
    {
        // COINIT_APARTMENTTHREADED (0x2). RPC_E_CHANGED_MODE (0x80010106) means the thread is
        // already initialised in another model, which is fine - we just skip the matching uninit.
        int initHr = CoInitializeEx(nint.Zero, 0x2);
        bool shouldUninitialise = initHr >= 0;

        nint shellLink = nint.Zero;
        nint persistFile = nint.Zero;

        try {
            if (CoCreateInstance(
                    in CLSID_ShellLink,
                    nint.Zero,
                    CLSCTX_INPROC_SERVER,
                    in IID_IShellLinkW,
                    out shellLink) < 0) {

                return null;
            }

            // IUnknown::QueryInterface is vtable slot 0. Called directly rather than through
            // Marshal, to stay off the COM marshaller entirely (see the type comment).
            Guid persistFileIid = IID_IPersistFile;

            int queryHr = ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)
                Vtbl(shellLink)[0])(shellLink, &persistFileIid, &persistFile);

            if (queryHr < 0) {
                return null;
            }

            fixed (char* pFileName = shortcutPath) {
                int loadHr = ((delegate* unmanaged[Stdcall]<nint, char*, uint, int>)
                    Vtbl(persistFile)[SlotLoad])(persistFile, pFileName, STGM_READ);

                if (loadHr < 0) {
                    return null;
                }
            }

            string path = GetString(shellLink, SlotGetPath, MAX_PATH, withFindData: true);

            if (path.Length == 0) {
                return null;
            }

            string arguments = GetString(shellLink, SlotGetArguments, InfoTipSize, withFindData: false);

            return new Target(path, arguments);
        }
        catch (Exception ex) {
            Trace.WriteLine($"{nameof(ShellLink)}.{nameof(Resolve)} failed for {shortcutPath}: {ex.Message}");
            return null;
        }
        finally {
            if (persistFile != nint.Zero) {
                Marshal.Release(persistFile);
            }

            if (shellLink != nint.Zero) {
                Marshal.Release(shellLink);
            }

            if (shouldUninitialise) {
                CoUninitialize();
            }
        }
    }

    private static string GetString(nint shellLink, int slot, int capacity, bool withFindData)
    {
        char* buffer = stackalloc char[capacity];
        new Span<char>(buffer, capacity).Clear();

        int hr;

        if (withFindData) {
            byte* findData = stackalloc byte[Win32FindDataSize];

            hr = ((delegate* unmanaged[Stdcall]<nint, char*, int, byte*, uint, int>)
                Vtbl(shellLink)[slot])(shellLink, buffer, capacity, findData, SLGP_RAWPATH);
        }
        else {
            hr = ((delegate* unmanaged[Stdcall]<nint, char*, int, int>)
                Vtbl(shellLink)[slot])(shellLink, buffer, capacity);
        }

        // S_FALSE (1) from GetPath means the link has no file-system target.
        return hr == 0 ? new string(buffer) : string.Empty;
    }

    private static void** Vtbl(nint pUnknown) => *(void***)pUnknown;
}
