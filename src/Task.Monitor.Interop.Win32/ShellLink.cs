namespace Task.Monitor.Interop.Win32;

// ShellLink is bound through its raw vtable rather than through [ComImport] interfaces.
// taskmon publishes with PublishAot=true, and Native AOT has no built-in COM marshalling.
// We must call the vtable directly.
public static unsafe class ShellLink
{
    public static readonly Guid CLSID_ShellLink  = new("00021401-0000-0000-C000-000000000046");
    public static readonly Guid IID_IShellLinkW  = new("000214F9-0000-0000-C000-000000000046");
    public static readonly Guid IID_IPersistFile = new("0000010B-0000-0000-C000-000000000046");

    public const uint STGM_READ = 0;

    public const uint SLGP_RAWPATH = 0x4;

    public const int INFOTIPSIZE       = 1024;
    public const int Win32FindDataSize = 592;

    // IShellLinkW Vtable slots.
    private const int SlotGetPath = 3;
    private const int SlotGetArguments = 10;

    // IPersistFile Vtable slots.
    private const int SlotLoad = 5;

    public static int IPersistFile_Load(nint persistFile, char* fileName, uint mode) =>
        ((delegate* unmanaged[Stdcall]<nint, char*, uint, int>)Ole32.Vtbl(persistFile)[SlotLoad])(persistFile, fileName, mode);

    public static int IShellLinkW_GetArguments(nint shellLink, char* arguments, int capacity) =>
        ((delegate* unmanaged[Stdcall]<nint, char*, int, int>)
            Ole32.Vtbl(shellLink)[SlotGetArguments])(shellLink, arguments, capacity);

    public static int IShellLinkW_GetPath(nint shellLink, char* file, int capacity, byte* findData, uint flags) =>
        ((delegate* unmanaged[Stdcall]<nint, char*, int, byte*, uint, int>)
            Ole32.Vtbl(shellLink)[SlotGetPath])(shellLink, file, capacity, findData, flags);
}
