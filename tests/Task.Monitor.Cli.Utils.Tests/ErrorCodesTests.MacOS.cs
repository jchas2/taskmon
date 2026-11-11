#if __APPLE__
namespace Task.Monitor.Cli.Utils.Tests;

public sealed partial class ErrorCodesTests
{
    [Fact]
    public void DescribeKernReturn_Names_A_Mach_Failure()
    {
        // KERN_FAILURE, which as an errno would read as EIO.
        string description = ErrorCodes.DescribeKernReturn(5);

        Assert.StartsWith("5 (", description);
        Assert.Contains("kern", description);
    }

    [Fact]
    public void DescribeKernReturn_Shows_An_IOReturn_In_Hex()
    {
        // kIOReturnNotPrivileged.
        string description = ErrorCodes.DescribeKernReturn(unchecked((int)0xE00002C1));

        Assert.StartsWith("0xE00002C1 (", description);
    }

    [Fact]
    public void Describe_Adds_The_Message_For_An_IOKit_Code()
    {
        // kIOReturnNotPrivileged.
        string description = ErrorCodes.Describe(0xE00002C1u);

        Assert.StartsWith("0xE00002C1 (", description);
        Assert.Contains("iokit", description);
    }

    [Fact]
    public void Describe_Shows_An_Errno_In_Decimal_With_Its_Message()
    {
        string description = ErrorCodes.Describe(2);

        Assert.StartsWith("2 (", description);
        Assert.EndsWith(")", description);
    }
}
#endif
