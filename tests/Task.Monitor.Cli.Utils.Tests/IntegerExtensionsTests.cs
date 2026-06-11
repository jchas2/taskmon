namespace Task.Monitor.Cli.Utils.Tests;

public sealed class IntegerExtensionsTests
{
    public static TheoryData<long, string> ByteData()
        => new()
        {
            { 1, "1 B" },
            { 16, "16 B" },
            { 32, "32 B" },
            { 64, "64 B" },
            { 264, "264 B" },
            { 512, "512 B" },
            { 1024, "1 KB" },
            { 1536, "1.5 KB" },
            { 1024 * 2, "2 KB" },
            { 1024 * 512, "512 KB" },
            { 1024 * 1024, "1 MB" },
            { 1024 * 1024 * 1024, "1 GB" }
        };
    
    [Theory]
    [MemberData(nameof(ByteData))]
    public void Should_Format_ByteSize(long num, string expected)
    {
        string value = num.ToFormattedByteSize();
        Assert.Equal(expected, value);
    }

    public static TheoryData<long, string> HexDataLong()
        => new()
        {
            { 123456789,                  "0x00000000075BCD15" },
            { 9_223_372_036_854_775_807,  "0x7FFFFFFFFFFFFFFF" },
        };
    
    public static TheoryData<ulong, string> HexDataULong()
        => new()
        {
            { 123456789,                  "0x00000000075BCD15" },
            { 9_223_372_036_854_775_807,  "0x7FFFFFFFFFFFFFFF" },
            { 18_446_744_073_709_551_615, "0xFFFFFFFFFFFFFFFF" }
        };

    [Theory]
    [MemberData(nameof(HexDataLong))]
    public void Should_Format_Long_Hex(long num, string expected)
    {
        string longValue = num.ToHexadecimal();
        Assert.Equal(expected, longValue);
    }
    
    [Theory]
    [MemberData(nameof(HexDataULong))]
    public void Should_Format_ULong_Hex(ulong num, string expected)
    {
        string ulongValue = num.ToHexadecimal();
        Assert.Equal(expected, ulongValue);
    }
}