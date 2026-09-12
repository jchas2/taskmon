using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class StartupApprovedStateTests
{
    private static byte[] Blob(uint flags, long fileTime)
    {
        byte[] blob = new byte[12];
        BitConverter.GetBytes(flags).CopyTo(blob, 0);
        BitConverter.GetBytes(fileTime).CopyTo(blob, 4);
        return blob;
    }

    [Theory]
    [InlineData(0x02u)]
    [InlineData(0x06u)]
    public void Parses_An_Even_Flag_As_Enabled(uint flags)
    {
        (StartupEntryState state, DateTime? disabledOn) = StartupApprovedState.Parse(Blob(flags, 0));

        Assert.Equal(StartupEntryState.Enabled, state);
        Assert.Null(disabledOn);
    }

    [Fact]
    public void Parses_An_Odd_Flag_As_Disabled_With_Its_Timestamp()
    {
        DateTime disabledAt = new(2025, 3, 14, 9, 30, 0, DateTimeKind.Utc);

        (StartupEntryState state, DateTime? disabledOn) =
            StartupApprovedState.Parse(Blob(0x03, disabledAt.ToFileTimeUtc()));

        Assert.Equal(StartupEntryState.Disabled, state);
        Assert.Equal(disabledAt, disabledOn);
    }

    [Fact]
    public void Parses_A_Disabled_Entry_With_No_Timestamp()
    {
        (StartupEntryState state, DateTime? disabledOn) = StartupApprovedState.Parse(Blob(0x03, 0));

        Assert.Equal(StartupEntryState.Disabled, state);
        Assert.Null(disabledOn);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(11)]
    public void Treats_A_Short_Blob_As_Unknown(int length)
    {
        (StartupEntryState state, DateTime? disabledOn) = StartupApprovedState.Parse(new byte[length]);

        Assert.Equal(StartupEntryState.Unknown, state);
        Assert.Null(disabledOn);
    }

    [Fact]
    public void Ignores_A_Nonsense_Timestamp()
    {
        (StartupEntryState state, DateTime? disabledOn) =
            StartupApprovedState.Parse(Blob(0x03, long.MaxValue));

        Assert.Equal(StartupEntryState.Disabled, state);
        Assert.Null(disabledOn);
    }
}
