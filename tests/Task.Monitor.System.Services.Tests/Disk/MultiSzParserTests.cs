using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Tests.Disk;

public sealed class MultiSzParserTests
{
    [Fact]
    public void Should_Parse_Single_Value()
    {
        string[] values = MultiSzParser.Parse("C:\\\0\0".AsSpan());

        Assert.Equal(["C:\\"], values);
    }

    [Fact]
    public void Should_Parse_Multiple_Values()
    {
        string[] values = MultiSzParser.Parse("C:\\\0D:\\Data\\\0E:\\\0\0".AsSpan());

        Assert.Equal(["C:\\", "D:\\Data\\", "E:\\"], values);
    }

    [Fact]
    public void Should_Parse_No_Values_For_A_Volume_Without_Mount_Points()
    {
        string[] values = MultiSzParser.Parse("\0".AsSpan());

        Assert.Empty(values);
    }

    [Fact]
    public void Should_Parse_No_Values_For_An_Empty_Buffer()
    {
        string[] values = MultiSzParser.Parse(ReadOnlySpan<char>.Empty);

        Assert.Empty(values);
    }

    [Fact]
    public void Should_Stop_At_The_Terminator_And_Ignore_Trailing_Content()
    {
        // Anything past the double NUL is uninitialised buffer, not another value.
        string[] values = MultiSzParser.Parse("C:\\\0\0Z:\\\0\0".AsSpan());

        Assert.Equal(["C:\\"], values);
    }

    [Fact]
    public void Should_Not_Read_Past_An_Unterminated_Buffer()
    {
        string[] values = MultiSzParser.Parse("C:\\".AsSpan());

        Assert.Equal(["C:\\"], values);
    }
}
