namespace Task.Monitor.Cli.Utils.Tests;

public sealed class StringExtensionsTests
{
    [Fact]
    public void TerminalWidth_Of_Ascii_Equals_Char_Length()
    {
        Assert.Equal(5, "Hello".TerminalWidth());
    }

    [Fact]
    public void TerminalWidth_Counts_CJK_Characters_As_Two_Columns()
    {
        // "公司名稱" (Chinese: "company name") - 4 chars, each rendered as two terminal columns.
        Assert.Equal(8, "公司名稱".TerminalWidth());
    }

    [Fact]
    public void TerminalWidth_Handles_Mixed_Ascii_And_Wide_Text()
    {
        Assert.Equal(10, "TODO: 公司".TerminalWidth());
    }

    [Fact]
    public void TruncateToTerminalWidth_Keeps_Whole_Ascii_Text_That_Fits()
    {
        int length = "Hello".TruncateToTerminalWidth(10, out int width);

        Assert.Equal(5, length);
        Assert.Equal(5, width);
    }

    [Fact]
    public void TruncateToTerminalWidth_Truncates_Ascii_Text_By_Char_Count()
    {
        int length = "Hello World".TruncateToTerminalWidth(5, out int width);

        Assert.Equal(5, length);
        Assert.Equal(5, width);
    }

    [Fact]
    public void TruncateToTerminalWidth_Stops_Before_A_Wide_Char_That_Would_Overflow()
    {
        // "AB" + "公" (2 columns) = 4 columns, exceeds a budget of 3 - the wide char must not be
        // half-emitted, so only "AB" (2 columns) fits.
        int length = "AB公".TruncateToTerminalWidth(3, out int width);

        Assert.Equal(2, length);
        Assert.Equal(2, width);
    }

    [Fact]
    public void TruncateToTerminalWidth_Includes_A_Wide_Char_That_Exactly_Fits()
    {
        int length = "AB公".TruncateToTerminalWidth(4, out int width);

        Assert.Equal(3, length);
        Assert.Equal(4, width);
    }

    [Fact]
    public void TruncateToTerminalWidth_Of_Empty_Text_Is_Empty()
    {
        int length = "".TruncateToTerminalWidth(10, out int width);

        Assert.Equal(0, length);
        Assert.Equal(0, width);
    }
}
