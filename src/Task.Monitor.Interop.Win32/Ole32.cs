using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static class Ole32
{
    [DllImport(Libraries.Ole32, PreserveSig = true)]
    public static extern int CoInitializeEx(nint pvReserved, uint dwCoInit);

    [DllImport(Libraries.Ole32)]
    public static extern void CoUninitialize();

    [DllImport(Libraries.Ole32, PreserveSig = true)]
    public static extern int CoCreateInstance(
        in Guid  rclsid, 
        nint     pUnkOuter, 
        uint     dwClsContext, 
        in Guid  riid, 
        out nint ppv);

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

    [DllImport(Libraries.OleAut32)]
    public static extern nint SysAllocString([MarshalAs(UnmanagedType.LPWStr)] string value);

    [DllImport(Libraries.OleAut32)]
    public static extern void SysFreeString(nint bstr);
    
    [DllImport(Libraries.OleAut32)]
    public static extern unsafe int VariantClear(byte* variant);

    public static uint COINIT_MULTITHREADED     = 0x0;  // Runs on any thread.
    public static uint COINIT_APARTMENTTHREADED = 0x2;  // Only runs on the thread that created the object.
    
    public static int ComInitializeEx(uint dwCoInit) => CoInitializeEx(nint.Zero, dwCoInit);

    public static void ComUninitialize(int hResult)
    {
        if (hResult >= 0) {
            CoUninitialize();
        }    
    }
    
    public static unsafe void** Vtbl(nint pUnknown) => *(void***)pUnknown;
}