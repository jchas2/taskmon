#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class StartupTargetUserTests
{
    [Fact]
    public void A_Normal_User_Is_Always_Themselves()
    {
        Assert.Equal(501u, StartupTargetUser.ResolveUid(euid: 501, uid: 501, sudoUid: "502", consoleUid: 503));
    }

    [Fact]
    public void Under_Sudo_It_Is_The_User_Who_Ran_Sudo()
    {
        Assert.Equal(501u, StartupTargetUser.ResolveUid(euid: 0, uid: 0, sudoUid: "501", consoleUid: 502));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("not a uid")]
    public void As_Root_Without_A_Sudoer_It_Is_The_Console_User(string? sudoUid)
    {
        Assert.Equal(502u, StartupTargetUser.ResolveUid(euid: 0, uid: 0, sudoUid, consoleUid: 502));
    }

    [Fact]
    public void As_Root_With_Nobody_At_The_Console_It_Is_Root()
    {
        Assert.Equal(0u, StartupTargetUser.ResolveUid(euid: 0, uid: 0, sudoUid: null, consoleUid: null));
        Assert.Equal(0u, StartupTargetUser.ResolveUid(euid: 0, uid: 0, sudoUid: null, consoleUid: 0));
    }

    [SkippableFact]
    public void The_Current_User_Owns_Their_Session()
    {
        Skip.If(UniStd.geteuid() == 0, "Under sudo the target is the sudoer, not the caller");

        StartupTargetUser user = StartupTargetUser.Current();

        Assert.Equal(UniStd.getuid(), user.Uid);
        Assert.True(user.IsSessionOwner);
        Assert.True(Directory.Exists(user.HomeDirectory));
        Assert.NotNull(user.Uuid);
    }
}
#endif
