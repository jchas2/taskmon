using System.Runtime.InteropServices;

namespace Task.Monitor.Cli.Utils;

public readonly ref struct HGlobalScope
{
    public HGlobalScope(nint value) => Value = value;

    public static HGlobalScope Allocate(int byteCount) => new(Marshal.AllocHGlobal(byteCount));

    public nint Value { get; }

    public bool IsNull => Value == nint.Zero;

    public static implicit operator nint(in HGlobalScope scope) => scope.Value;

    public void Dispose()
    {
        if (Value != nint.Zero) {
            Marshal.FreeHGlobal(Value);
        }
    }
}
