using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.Interop.Win32;

// WMI (Windows implementation of the Web standard WBEM), is bound through its raw vtable rather than
// through [ComImport] interfaces.
// taskmon publishes with PublishAot=true, and Native AOT has no built-in COM marshalling.
// We must call the vtable directly.
public static unsafe class Wbem
{
    private static readonly Guid CLSID_WbemLocator = new("4590F811-1D3A-11D0-891F-00AA004B2E24");
    private static readonly Guid IID_IWbemLocator  = new("DC12A687-737F-11CF-884D-00AA004B2E24");

    private const uint CLSCTX_INPROC_SERVER = 1;

    private const uint RPC_C_AUTHN_LEVEL_DEFAULT = 0;
    private const uint RPC_C_AUTHN_LEVEL_CALL    = 3;
    private const uint RPC_C_AUTHN_WINNT         = 10;
    private const uint RPC_C_AUTHZ_NONE          = 0;
    
    private const uint RPC_C_IMP_LEVEL_IMPERSONATE = 3;
    private const uint EOAC_NONE = 0;

    private const int RPC_E_TOO_LATE = unchecked((int)0x80010119);
    private const int S_OK = 0;

    private const int WBEM_FLAG_FORWARD_ONLY       = 0x20;
    private const int WBEM_FLAG_RETURN_IMMEDIATELY = 0x10;
    
    private const int WBEM_INFINITE = unchecked((int)0xFFFFFFFF);

    // Vtable slots past IUnknown.
    private const int SlotLocatorConnectServer = 3;
    private const int SlotServicesExecQuery = 20;
    private const int SlotEnumNext = 4;
    private const int SlotObjectGet = 4;

    private const int VariantSize = 24;

    public static IReadOnlyList<Dictionary<string, object?>> Query(
        string wmiNamespace, 
        string wql, 
        params string[] properties)
    {
        List<Dictionary<string, object?>> rows = new();

        int hResult = Ole32.ComInitializeEx(Ole32.COINIT_MULTITHREADED);

        int secHr = Ole32.CoInitializeSecurity(
            securityDescriptor: nint.Zero, 
            authSvc: -1, 
            asAuthSvc: nint.Zero, 
            reserved1: nint.Zero,
            authnLevel: RPC_C_AUTHN_LEVEL_DEFAULT, 
            impLevel: RPC_C_IMP_LEVEL_IMPERSONATE, 
            authList: nint.Zero, 
            capabilities: EOAC_NONE, 
            reserved3: nint.Zero);

        if (secHr < 0 && secHr != RPC_E_TOO_LATE) {
            Ole32.ComUninitialize(hResult);
            return rows;
        }

        nint locator    = nint.Zero;
        nint services   = nint.Zero;
        nint enumerator = nint.Zero;
        
        nint namespaceBstr = Ole32.SysAllocString(wmiNamespace);
        nint languageBstr  = Ole32.SysAllocString("WQL");
        nint queryBstr     = Ole32.SysAllocString(wql);

        if (Ole32.CoCreateInstance(
            in CLSID_WbemLocator, 
            nint.Zero, 
            CLSCTX_INPROC_SERVER,
            in IID_IWbemLocator, 
            out locator) < 0 || locator == nint.Zero) {
            
            goto Finished;
        }

        int connectHr = ((delegate* unmanaged[Stdcall]<nint, nint, nint, nint, nint, int, nint, nint, nint*, int>)
            Ole32.Vtbl(locator)[SlotLocatorConnectServer])(
                locator, 
                namespaceBstr, 
                nint.Zero, 
                nint.Zero, 
                nint.Zero, 
                0, 
                nint.Zero, 
                nint.Zero, 
                &services);

        if (connectHr < 0 || services == nint.Zero) {
            goto Finished;
        }

        if (Ole32.CoSetProxyBlanket(
            proxy: services, 
            authnService: RPC_C_AUTHN_WINNT, 
            authzService: RPC_C_AUTHZ_NONE, 
            serverPrincipalName: nint.Zero,
            authnLevel: RPC_C_AUTHN_LEVEL_CALL, 
            impLevel: RPC_C_IMP_LEVEL_IMPERSONATE, 
            authInfo: nint.Zero, 
            capabilities: EOAC_NONE) < 0) {

            goto Finished;
        }

        int execHr = ((delegate* unmanaged[Stdcall]<nint, nint, nint, int, nint, nint*, int>)
            Ole32.Vtbl(services)[SlotServicesExecQuery])(
                services, 
                languageBstr, 
                queryBstr,
                WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY, 
                nint.Zero, 
                &enumerator);

        if (execHr < 0 || enumerator == nint.Zero) {
            goto Finished;
        }

        ReadRows(enumerator, properties, rows);
        
    Finished:
        InteropHelper.MarshalRelease(ref enumerator);
        InteropHelper.MarshalRelease(ref services);
        InteropHelper.MarshalRelease(ref locator);
        
        Ole32.SysFreeString(namespaceBstr);
        Ole32.SysFreeString(languageBstr);
        Ole32.SysFreeString(queryBstr);
        Ole32.ComUninitialize(hResult);
        
        return rows;
    }

    private static void ReadRows(
        nint enumerator, 
        string[] properties, 
        List<Dictionary<string, object?>> rows)
    {
        while (true) {
            nint classObject = nint.Zero;
            uint returned = 0;

            int hr = ((delegate* unmanaged[Stdcall]<nint, int, uint, nint*, uint*, int>)
                Ole32.Vtbl(enumerator)[SlotEnumNext])(
                    enumerator, 
                    WBEM_INFINITE, 
                    1, 
                    &classObject, 
                    &returned);

            if (hr != S_OK || returned == 0 || classObject == nint.Zero) {
                break;
            }

            Dictionary<string, object?> row = new(properties.Length);

            foreach (string property in properties) {
                row[property] = ReadProperty(classObject, property);
            }

            rows.Add(row);
            InteropHelper.MarshalRelease(ref classObject);
        }
    }

    private static object? ReadProperty(nint classObject, string property)
    {
        byte* variant = stackalloc byte[VariantSize];
        new Span<byte>(variant, VariantSize).Clear();

        fixed (char* name = property) {
            int hr = ((delegate* unmanaged[Stdcall]<nint, char*, int, byte*, nint, nint, int>)
                Ole32.Vtbl(classObject)[SlotObjectGet])(
                    classObject, 
                    name, 
                    0, 
                    variant, 
                    nint.Zero, 
                    nint.Zero);

            if (hr < 0) {
                return null;
            }
        }

        ushort vt = *(ushort*)variant;
        byte* value = variant + 8;

        object? result = vt switch {
            2 =>  *(short*)value,             // VT_I2
            3 or 22 => *(int*)value,          // VT_I4 / VT_INT
            4 =>  (double)*(float*)value,     // VT_R4
            5 =>  *(double*)value,            // VT_R8
            8 =>  InteropHelper.TryMarshalPtrToStringBSTR(*(nint*)value),     // VT_BSTR
            11 => *(short*)value != 0,        // VT_BOOL
            16 => *(sbyte*)value,             // VT_I1
            17 => *value,                     // VT_UI1
            18 => *(ushort*)value,            // VT_UI2
            19 or 23 => *(uint*)value,        // VT_UI4 / VT_UINT
            20 => *(long*)value,              // VT_I8
            21 => *(ulong*)value,             // VT_UI8
            _ => null                         // VT_EMPTY, VT_NULL, arrays, ...
        };
            
        Ole32.VariantClear(variant);
        return result;
    }
}
