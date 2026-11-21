#if __WIN32__
using System.Runtime.InteropServices;

namespace Task.Monitor.System.Native;

// Releases an owned COM interface pointer, one the method created or received through an out
// parameter.
public readonly ref struct ComScope
{
    public ComScope(nint value) => Value = value;

    public void Dispose()
    {
        if (Value != nint.Zero) {
            Marshal.Release(Value);
        }
    }

    public bool IsNull => Value == nint.Zero;

    public static implicit operator nint(in ComScope scope) => scope.Value;

    public nint Value { get; }
}
#endif
