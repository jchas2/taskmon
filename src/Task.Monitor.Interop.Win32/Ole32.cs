using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static unsafe class Ole32
{
    public const uint COINIT_MULTITHREADED     = 0x0;  // Runs on any thread.
    public const uint COINIT_APARTMENTTHREADED = 0x2;  // Only runs on the thread that created the object.

    public const uint CLSCTX_INPROC_SERVER = 1;

    public const int VariantSize = 24;

    private const int SlotQueryInterface = 0;

    [DllImport(Libraries.Ole32, PreserveSig = true)]
    public static extern int CoCreateInstance(
        in Guid  rclsid,
        nint     pUnkOuter,
        uint     dwClsContext,
        in Guid  riid,
        out nint ppv);

    [DllImport(Libraries.Ole32, PreserveSig = true)]
    public static extern int CoInitializeEx(nint pvReserved, uint dwCoInit);

    [DllImport(Libraries.Ole32, PreserveSig = true)]
    public static extern int CoInitializeSecurity(
        nint securityDescriptor,
        int  authSvc,
        nint asAuthSvc,
        nint reserved1,
        uint authnLevel,
        uint impLevel,
        nint authList,
        uint capabilities,
        nint reserved3);

    [DllImport(Libraries.Ole32, PreserveSig = true)]
    public static extern int CoSetProxyBlanket(
        nint proxy,
        uint authnService,
        uint authzService,
        nint serverPrincipalName,
        uint authnLevel,
        uint impLevel,
        nint authInfo,
        uint capabilities);

    [DllImport(Libraries.Ole32)]
    public static extern void CoUninitialize();

    public static int IUnknown_QueryInterface(nint unknown, Guid* iid, nint* result) =>
        ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Vtbl(unknown)[SlotQueryInterface])(unknown, iid, result);

    [DllImport(Libraries.OleAut32)]
    public static extern nint SysAllocString([MarshalAs(UnmanagedType.LPWStr)] string value);

    [DllImport(Libraries.OleAut32)]
    public static extern void SysFreeString(nint bstr);

    [DllImport(Libraries.OleAut32)]
    public static extern int VariantClear(byte* variant);

    public static void** Vtbl(nint pUnknown) => *(void***)pUnknown;
}
