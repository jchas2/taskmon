namespace Task.Monitor.Interop.Win32;

// WMI (Windows implementation of the Web standard WBEM), is bound through its raw vtable rather than
// through [ComImport] interfaces.
// taskmon publishes with PublishAot=true, and Native AOT has no built-in COM marshalling.
// We must call the vtable directly.
public static unsafe class Wbem
{
    public static readonly Guid CLSID_WbemLocator = new("4590F811-1D3A-11D0-891F-00AA004B2E24");
    public static readonly Guid IID_IWbemLocator  = new("DC12A687-737F-11CF-884D-00AA004B2E24");

    public const uint RPC_C_AUTHN_LEVEL_DEFAULT = 0;
    public const uint RPC_C_AUTHN_LEVEL_CALL    = 3;
    public const uint RPC_C_AUTHN_WINNT         = 10;
    public const uint RPC_C_AUTHZ_NONE          = 0;

    public const uint RPC_C_IMP_LEVEL_IMPERSONATE = 3;
    public const uint EOAC_NONE = 0;

    public const int RPC_E_TOO_LATE = unchecked((int)0x80010119);
    public const int S_OK = 0;

    public const int WBEM_FLAG_FORWARD_ONLY       = 0x20;
    public const int WBEM_FLAG_RETURN_IMMEDIATELY = 0x10;

    public const int WBEM_INFINITE = unchecked((int)0xFFFFFFFF);

    // Vtable slots past IUnknown.
    private const int SlotLocatorConnectServer = 3;
    private const int SlotServicesExecQuery = 20;
    private const int SlotEnumNext = 4;
    private const int SlotObjectGet = 4;

    public static int IEnumWbemClassObject_Next(nint enumerator, int timeout, uint count, nint* objects, uint* returned) =>
        ((delegate* unmanaged[Stdcall]<nint, int, uint, nint*, uint*, int>)
            Ole32.Vtbl(enumerator)[SlotEnumNext])(enumerator, timeout, count, objects, returned);

    public static int IWbemClassObject_Get(nint classObject, char* name, int flags, byte* value, nint type, nint flavor) =>
        ((delegate* unmanaged[Stdcall]<nint, char*, int, byte*, nint, nint, int>)
            Ole32.Vtbl(classObject)[SlotObjectGet])(classObject, name, flags, value, type, flavor);

    public static int IWbemLocator_ConnectServer(
        nint  locator,
        nint  networkResource,
        nint  user,
        nint  password,
        nint  locale,
        int   securityFlags,
        nint  authority,
        nint  context,
        nint* services) =>
        ((delegate* unmanaged[Stdcall]<nint, nint, nint, nint, nint, int, nint, nint, nint*, int>)
            Ole32.Vtbl(locator)[SlotLocatorConnectServer])(
                locator, networkResource, user, password, locale, securityFlags, authority, context, services);

    public static int IWbemServices_ExecQuery(nint services, nint queryLanguage, nint query, int flags, nint context, nint* enumerator) =>
        ((delegate* unmanaged[Stdcall]<nint, nint, nint, int, nint, nint*, int>)
            Ole32.Vtbl(services)[SlotServicesExecQuery])(services, queryLanguage, query, flags, context, enumerator);
}
