#if __WIN32__
namespace Task.Monitor.Cli.Utils.Tests;

public sealed partial class ErrorCodesTests
{
    // Only macOS has text for Mach codes.
    [Fact]
    public void DescribeKernReturn_Is_The_Number_Alone() =>
        Assert.Equal("0xE00002C1", ErrorCodes.DescribeKernReturn(unchecked((int)0xE00002C1)));

    [Theory]
    [InlineData(0x80010106u)]   // RPC_E_CHANGED_MODE, from the system table.
    [InlineData(0x887A0004u)]   // DXGI_ERROR_UNSUPPORTED, from the system table.
    [InlineData(0x800007D5u)]   // PDH_NO_DATA, from pdh.dll.
    [InlineData(0xC0000BB8u)]   // PDH_CSTATUS_NO_OBJECT, from pdh.dll.
    [InlineData(0x80041003u)]   // WBEM_E_ACCESS_DENIED, from wmiutils.dll.
    [InlineData(0xC0000022u)]   // STATUS_ACCESS_DENIED, from ntdll.dll.
    public void Describe_Adds_The_Message_For_A_Large_Code(uint code)
    {
        string description = ErrorCodes.Describe(code);

        Assert.StartsWith($"0x{code:X8} (", description);
        Assert.EndsWith(")", description);
        Assert.True(description.Length > "0x00000000 ()".Length, description);
    }

    [Fact]
    public void Describe_Joins_A_Message_That_Runs_Over_Two_Lines()
    {
        string description = ErrorCodes.Describe(0xC0000022u);

        Assert.DoesNotContain('\r', description);
        Assert.DoesNotContain('\n', description);
    }

    [Fact]
    public void Describe_Shows_A_Win32_Error_In_Decimal_With_Its_Message()
    {
        string description = ErrorCodes.Describe(5);

        Assert.StartsWith("5 (", description);
        Assert.EndsWith(")", description);
        Assert.DoesNotContain("Unknown error", description);
    }
}
#endif
