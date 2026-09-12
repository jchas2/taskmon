namespace Task.Monitor.Cli.Utils.Tests;

public sealed class IntegerExtensionsTests
{
    public static TheoryData<long, string> ByteData()
        => new()
        {
            { 1, "1.0 B" },
            { 16, "16.0 B" },
            { 32, "32.0 B" },
            { 64, "64.0 B" },
            { 264, "264.0 B" },
            { 512, "512.0 B" },
            { 1024, "1.0 KB" },
            { 1536, "1.5 KB" },
            { 1024 * 2, "2.0 KB" },
            { 1024 * 512, "512.0 KB" },
            { 1024 * 1024, "1.0 MB" },
            { 1024 * 1024 * 1024, "1.0 GB" },

            // The GPU memory figures, which are the reason the decimal place is fixed: the
            // dedicated and shared totals have to render as Task Manager shows them.
            { 12_878_610_432, "12.0 GB" },
            { 17_044_301_824, "15.9 GB" },
            { 29_922_912_256, "27.9 GB" }
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