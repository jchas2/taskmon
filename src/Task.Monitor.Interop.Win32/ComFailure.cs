using Task.Monitor.Cli.Utils;

namespace Task.Monitor.Interop.Win32;

public readonly record struct ComFailure(string Call, int HResult)
{
    public override string ToString() =>
        HResult < 0
            ? $"{Call} {ErrorCodes.Describe(HResult)}"
            : $"{Call} 0x{HResult:X8}";
}
