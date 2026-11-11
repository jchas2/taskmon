namespace Task.Monitor.Cli.Utils.Tests;

// The message text comes from the system and follows its display language, so these check where
// a description came from by its shape, and only the code itself exactly.
public sealed partial class ErrorCodesTests
{
    [Fact]
    public void Describe_Shows_Only_The_Hex_For_A_Large_Code_Nothing_Describes() =>
        Assert.Equal("0xA0001234", ErrorCodes.Describe(0xA0001234u));

    [Fact]
    public void Describe_Takes_A_Signed_HResult_As_The_Same_Code() =>
        Assert.Equal(
            ErrorCodes.Describe(0x80041003u),
            ErrorCodes.Describe(unchecked((int)0x80041003)));
}
