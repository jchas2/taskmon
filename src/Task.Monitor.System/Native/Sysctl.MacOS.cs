#if __APPLE__
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public static unsafe class Sysctl
{
    public static int? GetArgMax()
    {
        int* name   = stackalloc int[] { (int)Sys.Selectors.CTL_KERN, Sys.KERN_ARGMAX };
        int  value  = 0;
        nint length = sizeof(int);

        return Sys.Sysctl(name, 2, &value, &length) == 0 && value > 0 ? value : null;
    }

    public static string? GetProcessExecPath(int pid, byte[] buffer)
    {
        int* name   = stackalloc int[] { (int)Sys.Selectors.CTL_KERN, Sys.KERN_PROCARGS2, pid };
        nint length = buffer.Length;

        fixed (byte* value = buffer) {
            if (Sys.Sysctl(name, 3, value, &length) != 0) {
                return null;
            }
        }

        return ParseExecPath(buffer.AsSpan(0, (int)length));
    }

    public static string? GetString(string name)
    {
        nint len = nint.Zero;

        if (Sys.SysctlByName(name, null, &len, null, nint.Zero) != 0) {
            Trace.WriteLine($"Failed SysctlByName() &len: {name}");
            return null;
        }

        int byteLen = (int)len;

        if (byteLen <= 0) {
            return null;
        }

        using HGlobalScope scope = HGlobalScope.Allocate(byteLen);
        byte* buffer = (byte*)scope.Value;

        nint outLen = byteLen;

        if (Sys.SysctlByName(name, buffer, &outLen, null, nint.Zero) != 0) {
            Trace.WriteLine($"Failed SysctlByName() buffer: {name}");
            return null;
        }

        int strLen = (int)outLen;

        if (strLen > 0 && buffer[strLen - 1] == 0) {
            strLen--;
        }

        return Encoding.UTF8.GetString(buffer, strLen);
    }

    public static string? ParseExecPath(ReadOnlySpan<byte> procArgs)
    {
        if (procArgs.Length <= sizeof(int)) {
            return null;
        }

        ReadOnlySpan<byte> path = procArgs[sizeof(int)..];
        int                end  = path.IndexOf((byte)0);

        return end > 0
            ? Encoding.UTF8.GetString(path[..end])
            : null;
    }

    public static bool TryGetInt32(string name, out int value)
    {
        value         = 0;
        int    result = 0;
        nint   len    = sizeof(int);

        if (Sys.SysctlByName(name, &result, &len, null, nint.Zero) != 0) {
            Trace.WriteLine($"Failed SysctlByName() &result: {name}");
            return false;
        }

        value = result;
        return true;
    }

    public static bool TryGetInt64(string name, out long value)
    {
        value         = 0;
        long   result = 0;
        nint   len    = sizeof(long);

        if (Sys.SysctlByName(name, &result, &len, null, nint.Zero) != 0) {
            Trace.WriteLine($"Failed SysctlByName() &result: {name}");
            return false;
        }

        value = result;
        return true;
    }

    public static bool TryRead(ReadOnlySpan<int> name, ref byte* value, ref int len)
    {
        fixed (int* ptr = &MemoryMarshal.GetReference(name)) {
            return TryRead(ptr, name.Length, ref value, ref len);
        }
    }

    private static bool TryRead(int* name, int nameLength, ref byte* value, ref int len)
    {
        nint bytesLength = len;
        bool autoSize    = value == null && len == 0;
        int  ret;

        if (autoSize) {
            ret = Sys.Sysctl(name, nameLength, value, &bytesLength);

            if (ret != 0) {
                Trace.WriteLine($"Failed Sysctl() &bytesLength: {*name}");
                return false;
            }

            value = (byte*)Marshal.AllocHGlobal((int)bytesLength);
        }

        ret = Sys.Sysctl(name, nameLength, value, &bytesLength);

        while (autoSize && ret != 0 && Marshal.GetLastPInvokeError() == Sys.ENOMEM) {
            Marshal.FreeHGlobal((nint)value);
            value = null;

            if ((int)bytesLength == int.MaxValue) {
                Trace.WriteLine($"Failed Sysctl() &bytesLength Out of memory : {*name}");
                throw new OutOfMemoryException();
            }

            bytesLength = (int)bytesLength >= int.MaxValue / 2
                ? int.MaxValue
                : (int)bytesLength * 2;

            value = (byte*)Marshal.AllocHGlobal(bytesLength);

            ret = Sys.Sysctl(name, nameLength, value, &bytesLength);
        }

        if (ret != 0) {
            if (autoSize) {
                Marshal.FreeHGlobal((nint)value);
                value = null;
            }

            return false;
        }

        len = (int)bytesLength;

        return true;
    }
}
#endif
