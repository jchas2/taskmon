namespace Task.Monitor.Interop.Win32;

// Closes an owned kernel handle. The APIs that return one disagree on failure: CreateFileW and
// CreateToolhelp32Snapshot return INVALID_HANDLE_VALUE (-1), OpenProcess returns null. Neither is a
// handle to close.
public readonly ref struct KernelHandleScope
{
    public KernelHandleScope(nint value) => Value = value;

    public void Dispose()
    {
        if (!IsInvalid) {
            Kernel32.CloseHandle(Value);
        }
    }

    public bool IsInvalid => Value == nint.Zero || Value == Kernel32.INVALID_HANDLE_VALUE;

    public static implicit operator nint(in KernelHandleScope scope) => scope.Value;

    public nint Value { get; }
}
