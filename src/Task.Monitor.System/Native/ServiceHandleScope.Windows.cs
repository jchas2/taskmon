#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Native;

// Closes an owned Service Control Manager or service handle.
public readonly ref struct ServiceHandleScope
{
    public ServiceHandleScope(nint value) => Value = value;

    public void Dispose()
    {
        if (Value != nint.Zero) {
            WinService.CloseServiceHandle(Value);
        }
    }

    public bool IsNull => Value == nint.Zero;

    public static implicit operator nint(in ServiceHandleScope scope) => scope.Value;

    public nint Value { get; }
}
#endif
