namespace Task.Monitor.Interop.Mach;

public readonly ref struct IOObjectScope
{
    public IOObjectScope(uint value) => Value = value;

    public IOObjectScope(nint value) => Value = (uint)value;

    public uint Value { get; }

    public bool IsNull => Value == 0;

    public static implicit operator uint(in IOObjectScope scope) => scope.Value;

    public static implicit operator nint(in IOObjectScope scope) => (nint)scope.Value;

    public void Dispose()
    {
        if (Value != 0) {
            IOKit.IOObjectRelease(Value);
        }
    }
}
