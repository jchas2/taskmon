namespace Task.Monitor.Interop.Mach;

// Simplifies CFRelease code for owned CoreFoundation references. 
public readonly ref struct CFScope
{
    public CFScope(nint value) => Value = value;

    public nint Value { get; }

    public bool IsNull => Value == nint.Zero;

    public static implicit operator nint(in CFScope scope) => scope.Value;

    public void Dispose()
    {
        if (Value != nint.Zero) {
            CoreFoundation.CFRelease(Value);
        }
    }
}
