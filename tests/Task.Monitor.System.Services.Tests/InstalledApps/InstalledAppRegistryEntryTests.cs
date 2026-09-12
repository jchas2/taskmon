using Task.Monitor.System.Services.InstalledApps;

namespace Task.Monitor.System.Services.Tests.InstalledApps;

public sealed class InstalledAppRegistryEntryTests
{
    private static InstalledApp? Parse(
        string? displayName = "My App",
        string? displayVersion = "1.2.3",
        string? publisher = "Acme",
        string? installDate = "20260115",
        string? installLocation = @"C:\Program Files\My App",
        string? uninstallString = @"C:\Program Files\My App\uninstall.exe",
        string? quietUninstallString = null,
        int? estimatedSizeKb = 51200,
        int? systemComponent = null,
        string? parentKeyName = null,
        string? releaseType = null,
        InstalledAppScope scope = InstalledAppScope.Machine,
        string origin = "HKLM\\...\\Uninstall\\MyApp") =>
        InstalledAppRegistryEntry.Parse(
            displayName, displayVersion, publisher, installDate, installLocation,
            uninstallString, quietUninstallString, estimatedSizeKb, systemComponent,
            parentKeyName, releaseType, scope, origin);

    [Fact]
    public void Parses_A_Well_Formed_Entry()
    {
        InstalledApp? app = Parse();

        Assert.NotNull(app);
        Assert.Equal("My App", app.Name);
        Assert.Equal("1.2.3", app.Version);
        Assert.Equal("Acme", app.Publisher);
        Assert.Equal(new DateTime(2026, 1, 15), app.InstallDate);
        Assert.Equal(@"C:\Program Files\My App", app.InstallLocation);
        Assert.Equal(51200, app.EstimatedSizeKb);
        Assert.Equal(@"C:\Program Files\My App\uninstall.exe", app.UninstallCommand);
        Assert.Equal(InstalledAppScope.Machine, app.Scope);
        Assert.Equal("HKLM\\...\\Uninstall\\MyApp", app.Origin);
    }

    [Fact]
    public void Prefers_QuietUninstallString_Over_UninstallString()
    {
        InstalledApp? app = Parse(
            uninstallString: "uninstall.exe",
            quietUninstallString: "uninstall.exe /quiet");

        Assert.Equal("uninstall.exe /quiet", app!.UninstallCommand);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Returns_Null_When_DisplayName_Is_Missing(string? displayName) =>
        Assert.Null(Parse(displayName: displayName));

    [Fact]
    public void Returns_Null_For_A_System_Component()
    {
        Assert.Null(Parse(systemComponent: 1));
    }

    [Fact]
    public void Includes_An_Entry_With_SystemComponent_Zero()
    {
        Assert.NotNull(Parse(systemComponent: 0));
    }

    [Fact]
    public void Returns_Null_For_An_Entry_With_A_Parent_Key()
    {
        Assert.Null(Parse(parentKeyName: "{PARENT-GUID}"));
    }

    [Theory]
    [InlineData("Update")]
    [InlineData("Security Update")]
    [InlineData("Hotfix")]
    [InlineData("ServicePack")]
    public void Returns_Null_For_Update_Release_Types(string releaseType) =>
        Assert.Null(Parse(releaseType: releaseType));

    [Fact]
    public void Returns_Null_For_Malformed_InstallDate()
    {
        InstalledApp? app = Parse(installDate: "not-a-date");

        Assert.NotNull(app);
        Assert.Null(app.InstallDate);
    }

    [Fact]
    public void Missing_Optional_Fields_Are_Null()
    {
        InstalledApp? app = Parse(
            displayVersion: null,
            publisher: null,
            installDate: null,
            installLocation: null,
            uninstallString: null,
            quietUninstallString: null,
            estimatedSizeKb: null);

        Assert.NotNull(app);
        Assert.Null(app.Version);
        Assert.Null(app.Publisher);
        Assert.Null(app.InstallDate);
        Assert.Null(app.InstallLocation);
        Assert.Null(app.UninstallCommand);
        Assert.Null(app.EstimatedSizeKb);
    }

    [Fact]
    public void User_Scope_Is_Preserved()
    {
        InstalledApp? app = Parse(scope: InstalledAppScope.User);

        Assert.Equal(InstalledAppScope.User, app!.Scope);
    }
}
