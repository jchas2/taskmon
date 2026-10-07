#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class BackgroundItemsDatabaseTests
{
    private static BackgroundItemsDatabase Database() =>
        BackgroundItemsDatabase.Parse(PropertyList.ParseKeyedArchive(BackgroundItemsFixture.Bytes))
        ?? throw new InvalidOperationException("Fixture did not parse");

    [Fact]
    public void Groups_Records_By_User_And_Skips_Invalid_Ones()
    {
        BackgroundItemsDatabase database = Database();

        // NoDisposition is not a valid record.
        Assert.Equal(["Example", "Helper", "Based"], database.RecordsFor(BackgroundItemsFixture.UserUuid).Select(r => r.Name));
        Assert.Equal(["com.example.daemon"], database.RecordsFor(BackgroundItemsFixture.SystemUuid).Select(r => r.Name));
    }

    [Fact]
    public void Looks_Up_Users_Case_Insensitively_And_Returns_Nothing_For_Others()
    {
        BackgroundItemsDatabase database = Database();

        Assert.Equal(3, database.RecordsFor(BackgroundItemsFixture.UserUuid.ToLowerInvariant()).Count);
        Assert.Empty(database.RecordsFor("00000000-0000-0000-0000-000000000000"));
        Assert.Empty(database.RecordsFor(null));
    }

    [Fact]
    public void Reads_An_App_Record()
    {
        BackgroundItemRecord app = Database().RecordsFor(BackgroundItemsFixture.UserUuid)[0];

        Assert.Equal("Example Corp", app.DeveloperName);
        Assert.Equal("2.com.example.app", app.Identifier);
        Assert.Equal("com.example.app", app.BundleIdentifier);
        Assert.True(app.Is(BackgroundItemType.App));
        Assert.True(app.IsApproved);
        Assert.False(app.IsLegacy);
        Assert.Equal("/Applications/Example App.app", app.ResolvePath(null));
    }

    [Fact]
    public void Resolves_A_Login_Item_Against_Its_Parent_App()
    {
        BackgroundItemRecord helper = Database().RecordsFor(BackgroundItemsFixture.UserUuid)[1];

        Assert.True(helper.Is(BackgroundItemType.LoginItem));
        Assert.Equal("2.com.example.app", helper.ParentIdentifier);
        Assert.False(helper.IsApproved); // allowed, but not enabled
        Assert.Null(helper.ResolvePath(null));
        Assert.Equal(
            "/Applications/Example App.app/Contents/Library/LoginItems/Helper.app",
            helper.ResolvePath("/Applications/Example App.app"));
    }

    [Fact]
    public void Reads_A_Legacy_Daemon()
    {
        BackgroundItemRecord daemon = Database().RecordsFor(BackgroundItemsFixture.SystemUuid)[0];

        Assert.True(daemon.Is(BackgroundItemType.Daemon));
        Assert.True(daemon.IsLegacy);
        Assert.False(daemon.Is(BackgroundItemType.Agent));
        Assert.Equal("/Library/LaunchDaemons/com.example.daemon.plist", daemon.ResolvePath(null));
    }

    [Fact]
    public void Returns_Null_For_An_Archive_Without_Items_By_User()
    {
        Dictionary<string, object?> archive = new() {
            ["$archiver"] = "NSKeyedArchiver",
            ["$top"]      = new Dictionary<string, object?> { ["store"] = new PropertyListUid(1) },
            ["$objects"]  = new List<object?> { "$null", new Dictionary<string, object?> { ["somethingElse"] = 1L } }
        };

        Assert.Null(BackgroundItemsDatabase.Parse(archive));
        Assert.Null(BackgroundItemsDatabase.Parse(null));
    }

    [Theory]
    [InlineData(0xBL, true)]
    [InlineData(0xAL, false)]
    [InlineData(0x3L, true)]
    [InlineData(0x1L, false)]
    public void Is_Approved_Only_When_Enabled_And_Allowed(long disposition, bool expected)
    {
        BackgroundItemRecord record = new() { Disposition = (BackgroundItemDisposition)disposition };

        Assert.Equal(expected, record.IsApproved);
    }
}
#endif
