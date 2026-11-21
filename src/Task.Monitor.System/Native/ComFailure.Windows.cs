#if __WIN32__
namespace Task.Monitor.System.Native;

public readonly record struct ComFailure(string Call, int HResult)
{
    public override string ToString() =>
        HResult < 0
            ? $"{Call} {ErrorCodes.Describe(HResult)}"
            : $"{Call} 0x{HResult:X8}";
}
#endif
