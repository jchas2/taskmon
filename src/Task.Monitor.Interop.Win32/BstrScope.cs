namespace Task.Monitor.Interop.Win32;

// Frees an owned BSTR, one the method allocated or received through an out parameter.
public readonly ref struct BstrScope
{
    public BstrScope(nint value) => Value = value;

    public static BstrScope Allocate(string value) => new(Ole32.SysAllocString(value));

    public void Dispose()
    {
        if (Value != nint.Zero) {
            Ole32.SysFreeString(Value);
        }
    }

    public bool IsNull => Value == nint.Zero;

    public static implicit operator nint(in BstrScope scope) => scope.Value;

    public nint Value { get; }
}
