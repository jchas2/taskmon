using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

/// <summary>
/// A read-only WMI query, bound through the raw COM vtables of IWbemLocator / IWbemServices /
/// IEnumWbemClassObject / IWbemClassObject. Same AOT-safe raw-pointer approach as <see cref="Dxgi"/>
/// and <see cref="ShellLink"/> - the built-in COM marshaller is not available under Native AOT.
///
/// Only what a monitor needs: run a WQL SELECT and read scalar properties (numbers and strings) off
/// each result object.
/// </summary>
public static unsafe class Wbem
{
    private static readonly Guid CLSID_WbemLocator = new("4590F811-1D3A-11D0-891F-00AA004B2E24");
    private static readonly Guid IID_IWbemLocator = new("DC12A687-737F-11CF-884D-00AA004B2E24");

    private const uint CLSCTX_INPROC_SERVER = 1;

    private const uint RPC_C_AUTHN_LEVEL_DEFAULT = 0;
    private const uint RPC_C_AUTHN_LEVEL_CALL = 3;
    private const uint RPC_C_IMP_LEVEL_IMPERSONATE = 3;
    private const uint RPC_C_AUTHN_WINNT = 10;
    private const uint RPC_C_AUTHZ_NONE = 0;
    private const uint EOAC_NONE = 0;

    private const int RPC_E_TOO_LATE = unchecked((int)0x80010119);
    private const int S_OK = 0;

    private const int WBEM_FLAG_FORWARD_ONLY = 0x20;
    private const int WBEM_FLAG_RETURN_IMMEDIATELY = 0x10;
    private const int WBEM_INFINITE = unchecked((int)0xFFFFFFFF);

    // Vtable slots past IUnknown.
    private const int SlotLocatorConnectServer = 3;
    private const int SlotServicesExecQuery = 20;
    private const int SlotEnumNext = 4;
    private const int SlotObjectGet = 4;

    // VARIANT is 24 bytes on x64: vt at +0, the value union at +8.
    private const int VariantSize = 24;

    [DllImport(Libraries.Ole32, PreserveSig = true)]
    private static extern int CoInitializeEx(nint reserved, uint coInit);

    [DllImport(Libraries.Ole32)]
    private static extern void CoUninitialize();

    [DllImport(Libraries.Ole32, PreserveSig = true)]
    private static extern int CoInitializeSecurity(
        nint securityDescriptor, int authSvc, nint asAuthSvc, nint reserved1,
        uint authnLevel, uint impLevel, nint authList, uint capabilities, nint reserved3);

    [DllImport(Libraries.Ole32, PreserveSig = true)]
    private static extern int CoCreateInstance(
        in Guid rclsid, nint outer, uint clsContext, in Guid riid, out nint ppv);

    [DllImport(Libraries.Ole32, PreserveSig = true)]
    private static extern int CoSetProxyBlanket(
        nint proxy, uint authnService, uint authzService, nint serverPrincipalName,
        uint authnLevel, uint impLevel, nint authInfo, uint capabilities);

    [DllImport(Libraries.OleAut32)]
    private static extern nint SysAllocString([MarshalAs(UnmanagedType.LPWStr)] string value);

    [DllImport(Libraries.OleAut32)]
    private static extern void SysFreeString(nint bstr);

    [DllImport(Libraries.OleAut32)]
    private static extern int VariantClear(byte* variant);

    /// <summary>
    /// Runs <paramref name="wql"/> against <paramref name="wmiNamespace"/> (e.g. <c>root\WMI</c>)
    /// and returns one dictionary per result object, each holding the requested
    /// <paramref name="properties"/> as <see cref="int"/> / <see cref="uint"/> / <see cref="double"/>
    /// / <see cref="string"/> / <c>null</c>. Returns an empty list on any failure.
    /// </summary>
    public static IReadOnlyList<Dictionary<string, object?>> Query(
        string wmiNamespace, string wql, params string[] properties)
    {
        List<Dictionary<string, object?>> rows = new();

        int initHr = CoInitializeEx(nint.Zero, 0 /* COINIT_MULTITHREADED */);
        bool shouldUninitialise = initHr >= 0;

        // Process-wide and one-shot: RPC_E_TOO_LATE just means someone got here first, which is fine.
        int secHr = CoInitializeSecurity(
            nint.Zero, -1, nint.Zero, nint.Zero,
            RPC_C_AUTHN_LEVEL_DEFAULT, RPC_C_IMP_LEVEL_IMPERSONATE, nint.Zero, EOAC_NONE, nint.Zero);

        if (secHr < 0 && secHr != RPC_E_TOO_LATE) {
            if (shouldUninitialise) {
                CoUninitialize();
            }

            return rows;
        }

        nint locator = nint.Zero;
        nint services = nint.Zero;
        nint enumerator = nint.Zero;
        nint namespaceBstr = SysAllocString(wmiNamespace);
        nint languageBstr = SysAllocString("WQL");
        nint queryBstr = SysAllocString(wql);

        try {
            if (CoCreateInstance(
                    in CLSID_WbemLocator, nint.Zero, CLSCTX_INPROC_SERVER,
                    in IID_IWbemLocator, out locator) < 0 || locator == nint.Zero) {

                return rows;
            }

            int connectHr = ((delegate* unmanaged[Stdcall]<nint, nint, nint, nint, nint, int, nint, nint, nint*, int>)
                Vtbl(locator)[SlotLocatorConnectServer])(
                    locator, namespaceBstr, nint.Zero, nint.Zero, nint.Zero, 0, nint.Zero, nint.Zero, &services);

            if (connectHr < 0 || services == nint.Zero) {
                return rows;
            }

            if (CoSetProxyBlanket(
                    services, RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE, nint.Zero,
                    RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE, nint.Zero, EOAC_NONE) < 0) {

                return rows;
            }

            int execHr = ((delegate* unmanaged[Stdcall]<nint, nint, nint, int, nint, nint*, int>)
                Vtbl(services)[SlotServicesExecQuery])(
                    services, languageBstr, queryBstr,
                    WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY, nint.Zero, &enumerator);

            if (execHr < 0 || enumerator == nint.Zero) {
                return rows;
            }

            ReadRows(enumerator, properties, rows);
            return rows;
        }
        catch (Exception ex) {
            System.Diagnostics.Trace.WriteLine($"{nameof(Wbem)}.{nameof(Query)} failed: {ex.Message}");
            return rows;
        }
        finally {
            if (enumerator != nint.Zero) {
                Marshal.Release(enumerator);
            }

            if (services != nint.Zero) {
                Marshal.Release(services);
            }

            if (locator != nint.Zero) {
                Marshal.Release(locator);
            }

            SysFreeString(namespaceBstr);
            SysFreeString(languageBstr);
            SysFreeString(queryBstr);

            if (shouldUninitialise) {
                CoUninitialize();
            }
        }
    }

    private static void ReadRows(
        nint enumerator, string[] properties, List<Dictionary<string, object?>> rows)
    {
        while (true) {
            nint classObject = nint.Zero;
            uint returned = 0;

            int hr = ((delegate* unmanaged[Stdcall]<nint, int, uint, nint*, uint*, int>)
                Vtbl(enumerator)[SlotEnumNext])(enumerator, WBEM_INFINITE, 1, &classObject, &returned);

            if (hr != S_OK || returned == 0 || classObject == nint.Zero) {
                break;
            }

            try {
                Dictionary<string, object?> row = new(properties.Length);

                foreach (string property in properties) {
                    row[property] = ReadProperty(classObject, property);
                }

                rows.Add(row);
            }
            finally {
                Marshal.Release(classObject);
            }
        }
    }

    private static object? ReadProperty(nint classObject, string property)
    {
        byte* variant = stackalloc byte[VariantSize];
        new Span<byte>(variant, VariantSize).Clear();

        fixed (char* name = property) {
            int hr = ((delegate* unmanaged[Stdcall]<nint, char*, int, byte*, nint, nint, int>)
                Vtbl(classObject)[SlotObjectGet])(classObject, name, 0, variant, nint.Zero, nint.Zero);

            if (hr < 0) {
                return null;
            }
        }

        try {
            ushort vt = *(ushort*)variant;
            byte* value = variant + 8;

            return vt switch {
                2 => *(short*)value,                              // VT_I2
                3 or 22 => *(int*)value,                          // VT_I4 / VT_INT
                4 => (double)*(float*)value,                      // VT_R4
                5 => *(double*)value,                             // VT_R8
                8 => Marshal.PtrToStringBSTR(*(nint*)value),      // VT_BSTR
                11 => *(short*)value != 0,                        // VT_BOOL
                16 => *(sbyte*)value,                             // VT_I1
                17 => *value,                                     // VT_UI1
                18 => *(ushort*)value,                            // VT_UI2
                19 or 23 => *(uint*)value,                        // VT_UI4 / VT_UINT
                20 => *(long*)value,                              // VT_I8
                21 => *(ulong*)value,                             // VT_UI8
                _ => null                                         // VT_EMPTY, VT_NULL, arrays, ...
            };
        }
        finally {
            VariantClear(variant);
        }
    }

    private static void** Vtbl(nint pUnknown) => *(void***)pUnknown;
}
