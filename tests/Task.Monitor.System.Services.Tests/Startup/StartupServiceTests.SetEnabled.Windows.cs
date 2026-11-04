#if __WIN32__
using Microsoft.Win32;
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

#pragma warning disable CA1416 // Validate platform compatibility

public sealed partial class StartupServiceTests
{
    private const string WindowsRunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    private static readonly DateTime DisabledAt = new(2026, 11, 4, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void ApprovedRunPathFor_Has_No_Approval_For_RunOnce()
    {
        Assert.Null(StartupService.ApprovedRunPathFor(RegistryHive.CurrentUser, RegistryView.Default, StartupEntrySource.RunOnceKey));
        Assert.Null(StartupService.ApprovedRunPathFor(RegistryHive.LocalMachine, RegistryView.Registry32, StartupEntrySource.RunOnceKey));
    }

    [Theory]
    [InlineData(RegistryHive.LocalMachine, RegistryView.Registry64, @"StartupApproved\Run")]
    [InlineData(RegistryHive.LocalMachine, RegistryView.Registry32, @"StartupApproved\Run32")]
    [InlineData(RegistryHive.CurrentUser,  RegistryView.Default,    @"StartupApproved\Run")]
    public void ApprovedRunPathFor_Keeps_The_32_Bit_Machine_Run_Key_Apart(RegistryHive hive, RegistryView view, string expectedEnd)
    {
        string? path = StartupService.ApprovedRunPathFor(hive, view, StartupEntrySource.RunKey);

        Assert.NotNull(path);
        Assert.EndsWith(expectedEnd, path);
    }

    [Theory]
    [InlineData(StartupEntryScope.Machine, RegistryHive.LocalMachine)]
    [InlineData(StartupEntryScope.User,    RegistryHive.CurrentUser)]
    public void ApprovedStartupFolderHive_Is_Machine_Wide_For_The_All_Users_Folder(StartupEntryScope scope, RegistryHive expected) =>
        Assert.Equal(expected, StartupService.ApprovedStartupFolderHive(scope));

    [Fact]
    public void BuildApprovedState_Disables_An_Entry_With_No_Value_And_Records_When()
    {
        byte[] state = StartupService.BuildApprovedState([], enabled: false, DisabledAt);

        (StartupEntryState parsedState, DateTime? disabledOn) = StartupService.ParseApprovedState(state);

        Assert.Equal(Blob(0x03, DisabledAt.ToFileTimeUtc()), state);
        Assert.Equal(StartupEntryState.Disabled, parsedState);
        Assert.Equal(DisabledAt, disabledOn);
    }

    [Fact]
    public void BuildApprovedState_Enables_An_Entry_And_Clears_The_Time()
    {
        byte[] state = StartupService.BuildApprovedState(Blob(0x03, DisabledAt.ToFileTimeUtc()), enabled: true, DisabledAt);

        (StartupEntryState parsedState, DateTime? disabledOn) = StartupService.ParseApprovedState(state);

        Assert.Equal(Blob(0x02, 0), state);
        Assert.Equal(StartupEntryState.Enabled, parsedState);
        Assert.Null(disabledOn);
    }

    [Theory]
    [InlineData(0x06u, false, 0x07u)]
    [InlineData(0x07u, true,  0x06u)]
    [InlineData(0x02u, false, 0x03u)]
    public void BuildApprovedState_Keeps_The_Other_Flag_Bits(uint flags, bool enabled, uint expectedFlags)
    {
        byte[] state = StartupService.BuildApprovedState(Blob(flags, 0), enabled, DisabledAt);

        Assert.Equal(expectedFlags, BitConverter.ToUInt32(state, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void BuildApprovedState_Replaces_A_Value_Too_Short_To_Hold_Flags(int length)
    {
        byte[] state = StartupService.BuildApprovedState(new byte[length], enabled: true, DisabledAt);

        Assert.Equal(Blob(0x02, 0), state);
    }

    private static (StartupEntryState State, DateTime? DisabledOnUtc) ReadApproval(string approvedPath, string valueName)
    {
        using RegistryKey baseKey      = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using RegistryKey? approvedKey = baseKey.OpenSubKey(approvedPath);

        return StartupService.ParseApprovedState(approvedKey?.GetValue(valueName) as byte[] ?? []);
    }

    [Fact]
    public void RunKeyOrigin_Names_WOW6432Node_For_The_32_Bit_Machine_View()
    {
        Assert.Equal(
            @"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
            StartupService.RunKeyOrigin(RegistryHive.LocalMachine, RegistryView.Registry32, WindowsRunKeyPath));

        Assert.Equal(
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            StartupService.RunKeyOrigin(RegistryHive.LocalMachine, RegistryView.Registry64, WindowsRunKeyPath));

        Assert.Equal(
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            StartupService.RunKeyOrigin(RegistryHive.CurrentUser, RegistryView.Default, WindowsRunKeyPath));
    }

    [Fact]
    public void SetEnabled_Does_Nothing_When_The_Entry_Is_Already_In_That_State()
    {
        // RunOnce can't be changed, so only an early return can succeed.
        StartupEntry entry = new() { Name = "Once", Source = StartupEntrySource.RunOnceKey, State = StartupEntryState.Enabled };

        Assert.Equal(StartupToggleResult.Succeeded, new StartupService().SetEnabled(entry, enabled: true));
    }

    [Fact]
    public void SetEnabled_Does_Not_Support_RunOnce_And_Leaves_The_Entry_Alone()
    {
        StartupEntry entry = new() { Name = "Once", Source = StartupEntrySource.RunOnceKey, State = StartupEntryState.Enabled };

        Assert.Equal(StartupToggleResult.NotSupported, new StartupService().SetEnabled(entry, enabled: false));
        Assert.Equal(StartupEntryState.Enabled, entry.State);
    }

    [Theory]
    [InlineData(@"\")]
    [InlineData(@"\taskmon-test-missing-folder")]
    public void SetEnabled_Reports_A_Missing_Scheduled_Task_As_Not_Found(string folderPath)
    {
        StartupEntry entry = new() {
            Name   = $"taskmon-test-missing-{Guid.NewGuid():N}",
            Source = StartupEntrySource.ScheduledTask,
            Origin = folderPath,
            State  = StartupEntryState.Enabled
        };

        Assert.Equal(StartupToggleResult.NotFound, new StartupService().SetEnabled(entry, enabled: false));
        Assert.Equal(StartupEntryState.Enabled, entry.State);
    }

    // Writes, and then removes, an approval for a made-up HKCU Run value: Explorer ignores an
    // approval with no Run value of the same name, so nothing starts or stops at sign-in.
    [Fact]
    public void SetEnabled_Writes_The_Per_User_Approval_Explorer_Reads()
    {
        string  valueName    = $"taskmon-test-{Guid.NewGuid():N}";
        string  approvedPath = StartupService.ApprovedRunPathFor(RegistryHive.CurrentUser, RegistryView.Default, StartupEntrySource.RunKey)!;

        StartupEntry entry = new() {
            Name     = valueName,
            Source   = StartupEntrySource.RunKey,
            Scope    = StartupEntryScope.User,
            State    = StartupEntryState.Enabled,
            Approval = new StartupApproval(RegistryHive.CurrentUser, approvedPath, valueName)
        };

        StartupService service = new();

        try {
            Assert.Equal(StartupToggleResult.Succeeded, service.SetEnabled(entry, enabled: false));
            Assert.Equal(StartupEntryState.Disabled, entry.State);
            Assert.NotNull(entry.DisabledOnUtc);
            Assert.Equal(StartupEntryState.Disabled, ReadApproval(approvedPath, valueName).State);

            Assert.Equal(StartupToggleResult.Succeeded, service.SetEnabled(entry, enabled: true));
            Assert.Equal(StartupEntryState.Enabled, entry.State);
            Assert.Null(entry.DisabledOnUtc);
            Assert.Equal(StartupEntryState.Enabled, ReadApproval(approvedPath, valueName).State);
        }
        finally {
            using RegistryKey baseKey      = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using RegistryKey? approvedKey = baseKey.OpenSubKey(approvedPath, writable: true);
            approvedKey?.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    [Theory]
    [InlineData(0,                        StartupToggleResult.Succeeded)]
    [InlineData(1,                        StartupToggleResult.Succeeded)]
    [InlineData(unchecked((int)0x80070005), StartupToggleResult.AccessDenied)]
    [InlineData(unchecked((int)0x80070002), StartupToggleResult.NotFound)]
    [InlineData(unchecked((int)0x80070003), StartupToggleResult.NotFound)]
    [InlineData(unchecked((int)0x80004005), StartupToggleResult.Failed)]
    public void ToToggleResult_Maps_The_HResult(int hResult, StartupToggleResult expected) =>
        Assert.Equal(expected, StartupService.ToToggleResult(hResult));
}

#pragma warning restore CA1416 // Validate platform compatibility
#endif
