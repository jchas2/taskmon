#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Native;

public static unsafe class WmiClient
{
    public static IReadOnlyList<Dictionary<string, object?>> Query(
        string wmiNamespace,
        string wql,
        params string[] properties) =>
        Query(wmiNamespace, wql, out _, properties);

    public static IReadOnlyList<Dictionary<string, object?>> Query(
        string          wmiNamespace,
        string          wql,
        out ComFailure? failure,
        params string[] properties)
    {
        List<Dictionary<string, object?>> rows = new();

        failure = null;

        using ComInitScope com = ComInitScope.Initialise(Ole32.COINIT_MULTITHREADED);

        int secHr = Ole32.CoInitializeSecurity(
            securityDescriptor: nint.Zero,
            authSvc: -1,
            asAuthSvc: nint.Zero,
            reserved1: nint.Zero,
            authnLevel: Wbem.RPC_C_AUTHN_LEVEL_DEFAULT,
            impLevel: Wbem.RPC_C_IMP_LEVEL_IMPERSONATE,
            authList: nint.Zero,
            capabilities: Wbem.EOAC_NONE,
            reserved3: nint.Zero);

        if (secHr < 0 && secHr != Wbem.RPC_E_TOO_LATE) {
            failure = new ComFailure(nameof(Ole32.CoInitializeSecurity), secHr);
            return rows;
        }

        using BstrScope namespaceBstr = BstrScope.Allocate(wmiNamespace);
        using BstrScope languageBstr  = BstrScope.Allocate("WQL");
        using BstrScope queryBstr     = BstrScope.Allocate(wql);

        int createHr = Ole32.CoCreateInstance(
            in Wbem.CLSID_WbemLocator,
            nint.Zero,
            Ole32.CLSCTX_INPROC_SERVER,
            in Wbem.IID_IWbemLocator,
            out nint locatorRef);

        using ComScope locator = new(locatorRef);

        if (createHr < 0 || locator.IsNull) {
            failure = new ComFailure($"{nameof(Ole32.CoCreateInstance)} WbemLocator", createHr);
            return rows;
        }

        nint servicesRef = nint.Zero;

        int connectHr = Wbem.IWbemLocator_ConnectServer(
            locator,
            namespaceBstr,
            nint.Zero,
            nint.Zero,
            nint.Zero,
            0,
            nint.Zero,
            nint.Zero,
            &servicesRef);

        using ComScope services = new(servicesRef);

        if (connectHr < 0 || services.IsNull) {
            failure = new ComFailure($"IWbemLocator::ConnectServer {wmiNamespace}", connectHr);
            return rows;
        }

        int blanketHr = Ole32.CoSetProxyBlanket(
            proxy: services,
            authnService: Wbem.RPC_C_AUTHN_WINNT,
            authzService: Wbem.RPC_C_AUTHZ_NONE,
            serverPrincipalName: nint.Zero,
            authnLevel: Wbem.RPC_C_AUTHN_LEVEL_CALL,
            impLevel: Wbem.RPC_C_IMP_LEVEL_IMPERSONATE,
            authInfo: nint.Zero,
            capabilities: Wbem.EOAC_NONE);

        if (blanketHr < 0) {
            failure = new ComFailure(nameof(Ole32.CoSetProxyBlanket), blanketHr);
            return rows;
        }

        nint enumeratorRef = nint.Zero;

        int execHr = Wbem.IWbemServices_ExecQuery(
            services,
            languageBstr,
            queryBstr,
            Wbem.WBEM_FLAG_FORWARD_ONLY | Wbem.WBEM_FLAG_RETURN_IMMEDIATELY,
            nint.Zero,
            &enumeratorRef);

        using ComScope enumerator = new(enumeratorRef);

        if (execHr < 0 || enumerator.IsNull) {
            failure = new ComFailure("IWbemServices::ExecQuery", execHr);
            return rows;
        }

        int nextHr = ReadRows(enumerator, properties, rows);

        if (nextHr < 0) {
            failure = new ComFailure("IEnumWbemClassObject::Next", nextHr);
        }

        return rows;
    }

    private static object? ReadProperty(nint classObject, string property)
    {
        byte* variant = stackalloc byte[Ole32.VariantSize];
        new Span<byte>(variant, Ole32.VariantSize).Clear();

        fixed (char* name = property) {
            if (Wbem.IWbemClassObject_Get(classObject, name, 0, variant, nint.Zero, nint.Zero) < 0) {
                return null;
            }
        }

        ushort vt = *(ushort*)variant;
        byte* value = variant + 8;

        object? result = vt switch {
            2        => *(short*)value,              // VT_I2
            3 or 22  => *(int*)value,                // VT_I4 / VT_INT
            4        => (double)*(float*)value,      // VT_R4
            5        => *(double*)value,             // VT_R8
            8        => InteropHelper.TryMarshalPtrToStringBSTR(*(nint*)value),     // VT_BSTR
            11       => *(short*)value != 0,         // VT_BOOL
            16       => *(sbyte*)value,              // VT_I1
            17       => *value,                      // VT_UI1
            18       => *(ushort*)value,             // VT_UI2
            19 or 23 => *(uint*)value,               // VT_UI4 / VT_UINT
            20       => *(long*)value,               // VT_I8
            21       => *(ulong*)value,              // VT_UI8
            _        => null                         // VT_EMPTY, VT_NULL, arrays, ...
        };

        Ole32.VariantClear(variant);
        return result;
    }

    private static int ReadRows(
        nint enumerator,
        string[] properties,
        List<Dictionary<string, object?>> rows)
    {
        while (true) {
            nint classObjectRef = nint.Zero;
            uint returned       = 0;

            int hr = Wbem.IEnumWbemClassObject_Next(enumerator, Wbem.WBEM_INFINITE, 1, &classObjectRef, &returned);

            using ComScope classObject = new(classObjectRef);

            if (hr != Wbem.S_OK || returned == 0 || classObject.IsNull) {
                return hr;
            }

            Dictionary<string, object?> row = new(properties.Length);

            foreach (string property in properties) {
                row[property] = ReadProperty(classObject, property);
            }

            rows.Add(row);
        }
    }
}
#endif
