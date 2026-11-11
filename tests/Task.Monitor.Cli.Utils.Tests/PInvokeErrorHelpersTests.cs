using System.Runtime.InteropServices;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Cli.Utils.Tests;

public sealed class PInvokeErrorHelpersTests
{
    [Fact]
    public void TraceOnceOnLastError_Keeps_The_Message()
    {
        string key = $"{nameof(TraceOnceOnLastError_Keeps_The_Message)} {Guid.NewGuid()}";

        using TraceCapture capture = new();
        Marshal.SetLastPInvokeError(5);
        PInvokeErrorHelpers.TraceOnceOnLastError(key, "Failed to open process for pid 1424");

        string line = Assert.Single(capture.LinesContaining(key));
        Assert.StartsWith($"Key: {key}: PINVOKE ERROR 5 (", line);
        Assert.EndsWith("): Failed to open process for pid 1424", line);
    }

    [Fact]
    public void TraceOnceOnLastError_Without_A_Message_Ends_At_The_Description()
    {
        string key = $"{nameof(TraceOnceOnLastError_Without_A_Message_Ends_At_The_Description)} {Guid.NewGuid()}";

        using TraceCapture capture = new();
        Marshal.SetLastPInvokeError(5);
        PInvokeErrorHelpers.TraceOnceOnLastError(key);

        string line = Assert.Single(capture.LinesContaining(key));
        Assert.StartsWith($"Key: {key}: PINVOKE ERROR 5 (", line);
        Assert.EndsWith(")", line);
    }

    [Fact]
    public void TraceOnHResult_Shows_The_Code_In_Hex()
    {
        string message = $"{nameof(TraceOnHResult_Shows_The_Code_In_Hex)} {Guid.NewGuid()}";

        using TraceCapture capture = new();
        PInvokeErrorHelpers.TraceOnHResult(message, unchecked((int)0xA0001234));

        Assert.Equal($"HRESULT ERROR 0xA0001234: {message}", Assert.Single(capture.LinesContaining(message)));
    }

    // PDH reports its status through the PInvoke helpers. It used to come out in decimal.
    [Fact]
    public void TraceOnPInvokeError_Shows_A_Large_Code_In_Hex()
    {
        string message = $"{nameof(TraceOnPInvokeError_Shows_A_Large_Code_In_Hex)} {Guid.NewGuid()}";

        using TraceCapture capture = new();
        PInvokeErrorHelpers.TraceOnPInvokeError(message, 0xA0001234);

        Assert.Equal($"PINVOKE ERROR 0xA0001234: {message}", Assert.Single(capture.LinesContaining(message)));
    }
}
