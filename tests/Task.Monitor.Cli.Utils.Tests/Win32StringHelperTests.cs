using System.Text;
using Microsoft.Win32;

namespace Task.Monitor.Cli.Utils.Tests;

public sealed class Win32StringHelperTests
{
    [Fact]
    public void ParseMultiSz_Should_Parse_Single_Value()
    {
        string[] values = Win32StringHelper.ParseMultiSz("C:\\\0\0".AsSpan());

        Assert.Equal(["C:\\"], values);
    }

    [Fact]
    public void ParseMultiSz_Should_Parse_Multiple_Values()
    {
        string[] values = Win32StringHelper.ParseMultiSz("C:\\\0D:\\Data\\\0E:\\\0\0".AsSpan());

        Assert.Equal(["C:\\", "D:\\Data\\", "E:\\"], values);
    }

    [Fact]
    public void ParseMultiSz_Should_Parse_No_Values_For_A_Volume_Without_Mount_Points()
    {
        string[] values = Win32StringHelper.ParseMultiSz("\0".AsSpan());

        Assert.Empty(values);
    }

    [Fact]
    public void ParseMultiSz_Should_Parse_No_Values_For_An_Empty_Buffer()
    {
        string[] values = Win32StringHelper.ParseMultiSz(ReadOnlySpan<char>.Empty);

        Assert.Empty(values);
    }

    [Fact]
    public void ParseMultiSz_Should_Stop_At_The_Terminator_And_Ignore_Trailing_Content()
    {
        // Anything past the double NUL is uninitialised buffer, not another value.
        string[] values = Win32StringHelper.ParseMultiSz("C:\\\0\0Z:\\\0\0".AsSpan());

        Assert.Equal(["C:\\"], values);
    }

    [Fact]
    public void ParseMultiSz_Should_Not_Read_Past_An_Unterminated_Buffer()
    {
        string[] values = Win32StringHelper.ParseMultiSz("C:\\".AsSpan());

        Assert.Equal(["C:\\"], values);
    }

    [Fact]
    public void FromNullTerminated_Should_Stop_At_The_First_Terminator()
    {
        // Fixed size buffers carry stale content after the terminator.
        Assert.Equal("NTFS", Win32StringHelper.FromNullTerminated("NTFS\0FAT32\0".AsSpan()));
    }

    [Fact]
    public void FromNullTerminated_Should_Not_Read_Past_An_Unterminated_Buffer()
    {
        Assert.Equal("explorer.exe", Win32StringHelper.FromNullTerminated("explorer.exe".AsSpan()));
    }

    [Fact]
    public void FromNullTerminated_Should_Return_Empty_For_A_Leading_Terminator()
    {
        Assert.Equal(string.Empty, Win32StringHelper.FromNullTerminated("\0Label".AsSpan()));
    }

    [Fact]
    public void FromNullTerminated_Should_Return_Empty_For_An_Empty_Buffer()
    {
        Assert.Equal(string.Empty, Win32StringHelper.FromNullTerminated(ReadOnlySpan<char>.Empty));
    }

    [Fact]
    public void FromNullTerminatedAnsi_Should_Stop_At_The_First_Terminator()
    {
        byte[] buffer = Encoding.ASCII.GetBytes("Samsung\0SSD 990 PRO\0");

        Assert.Equal("Samsung", Win32StringHelper.FromNullTerminatedAnsi(buffer));
    }

    [Fact]
    public void FromNullTerminatedAnsi_Should_Not_Read_Past_An_Unterminated_Buffer()
    {
        byte[] buffer = Encoding.ASCII.GetBytes("4B2QJXD7");

        Assert.Equal("4B2QJXD7", Win32StringHelper.FromNullTerminatedAnsi(buffer));
    }

    [Fact]
    public void FromNullTerminatedAnsi_Should_Decode_High_Bit_Bytes_As_Latin1()
    {
        byte[] buffer = [0x43, 0x61, 0x66, 0xE9, 0x00];

        Assert.Equal("Café", Win32StringHelper.FromNullTerminatedAnsi(buffer));
    }

    [Fact]
    public void FromNullTerminatedAnsi_Should_Not_Trim_Whitespace()
    {
        // Space padded STORAGE_DEVICE_DESCRIPTOR fields are trimmed by the caller.
        byte[] buffer = Encoding.ASCII.GetBytes("  WDC  \0");

        Assert.Equal("  WDC  ", Win32StringHelper.FromNullTerminatedAnsi(buffer));
    }

    // Formats the enum only, no Registry access, so safe to run on every platform.
#pragma warning disable CA1416 // Validate platform compatibility
    [Theory]
    [InlineData(RegistryHive.LocalMachine, "HKLM")]
    [InlineData(RegistryHive.CurrentUser, "HKCU")]
    [InlineData(RegistryHive.Users, "Users")]
    public void HiveShortName_Should_Abbreviate_Known_Hives(RegistryHive hive, string expected)
    {
        Assert.Equal(expected, Win32StringHelper.HiveShortName(hive));
    }
#pragma warning restore CA1416 // Validate platform compatibility
}
