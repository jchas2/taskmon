using Task.Monitor.System.Services.InstalledApps;

namespace Task.Monitor.System.Services.Tests.InstalledApps;

public sealed class InstalledAppBundleEntryTests
{
    private const string BundlePath = "/Applications/Visual Studio Code.app";

    [Fact]
    public void GetStamp_Changes_When_The_Info_Plist_Is_Rewritten()
    {
        string bundle = Directory.CreateTempSubdirectory("taskmon-stamp-").FullName;

        try {
            string contents = Directory.CreateDirectory(Path.Combine(bundle, "Contents")).FullName;
            string infoPlist = Path.Combine(contents, "Info.plist");
            File.WriteAllText(infoPlist, "<plist/>");

            Directory.SetLastWriteTimeUtc(bundle, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(infoPlist, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            DateTime before = InstalledAppBundleEntry.GetStamp(bundle);

            // An in-place update rewrites the Info.plist without touching the bundle folder itself.
            File.SetLastWriteTimeUtc(infoPlist, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.NotEqual(before, InstalledAppBundleEntry.GetStamp(bundle));
        }
        finally {
            Directory.Delete(bundle, recursive: true);
        }
    }

    private static Dictionary<string, object?> Info(params (string Key, object? Value)[] values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value);

    private static InstalledApp? Parse(
        Dictionary<string, object?>? info = null,
        Dictionary<string, object?>? localizedInfo = null,
        string bundlePath = BundlePath,
        InstalledAppScope scope = InstalledAppScope.Machine,
        string? signingPublisher = "Microsoft Corporation",
        DateTime? installDate = null,
        long? estimatedSizeKb = null,
        bool sizePending = true) =>
        InstalledAppBundleEntry.Parse(
            info ?? Info(("CFBundleShortVersionString", "1.109.5")),
            localizedInfo,
            bundlePath,
            scope,
            signingPublisher,
            installDate,
            estimatedSizeKb,
            sizePending);

    [Fact]
    public void Parse_Falls_Back_To_CFBundleVersion_When_There_Is_No_Short_Version()
    {
        InstalledApp? app = Parse(info: Info(("CFBundleVersion", "4200")));

        Assert.Equal("4200", app!.Version);
    }

    [Fact]
    public void Parse_Keeps_The_Signing_Publisher_Over_The_Copyright()
    {
        InstalledApp? app = Parse(
            info: Info(("NSHumanReadableCopyright", "Copyright (C) 2026 Microsoft. All rights reserved")),
            signingPublisher: "Microsoft Corporation");

        Assert.Equal("Microsoft Corporation", app!.Publisher);
    }

    [Fact]
    public void Parse_Maps_The_Bundle_Onto_An_InstalledApp()
    {
        DateTime added = new(2026, 3, 1);

        InstalledApp? app = Parse(
            scope: InstalledAppScope.User,
            installDate: added,
            estimatedSizeKb: 679731,
            sizePending: false);

        Assert.NotNull(app);
        Assert.Equal("Visual Studio Code", app.Name);
        Assert.Equal("1.109.5", app.Version);
        Assert.Equal("Microsoft Corporation", app.Publisher);
        Assert.Equal(added, app.InstallDate);
        Assert.Equal(BundlePath, app.InstallLocation);
        Assert.Equal(BundlePath, app.Origin);
        Assert.Equal(679731, app.EstimatedSizeKb);
        Assert.False(app.SizePending);
        Assert.Equal(InstalledAppScope.User, app.Scope);
        Assert.Null(app.UninstallCommand);
    }

    [Fact]
    public void Parse_Marks_The_Size_Pending_Until_It_Is_Measured()
    {
        InstalledApp? app = Parse(estimatedSizeKb: null, sizePending: true);

        Assert.True(app!.SizePending);
        Assert.Null(app.EstimatedSizeKb);
    }

    [Fact]
    public void Parse_Returns_Null_Without_An_Info_Plist()
    {
        InstalledApp? app = InstalledAppBundleEntry.Parse(
            info: null,
            localizedInfo: null,
            bundlePath: BundlePath,
            scope: InstalledAppScope.Machine,
            signingPublisher: null,
            installDate: null,
            estimatedSizeKb: null,
            sizePending: true);

        Assert.Null(app);
    }

    [Fact]
    public void Parse_Uses_The_Bundle_Folder_Name_Over_The_Info_Plist_Names()
    {
        // Visual Studio Code's Info.plist calls it "Code"; Finder, like taskmon, shows the folder name.
        InstalledApp? app = Parse(info: Info(
            ("CFBundleName", "Code"),
            ("CFBundleDisplayName", "Code"),
            ("CFBundleShortVersionString", "1.109.5")));

        Assert.Equal("Visual Studio Code", app!.Name);
    }

    [Fact]
    public void Parse_Uses_The_Copyright_For_An_App_Store_Build()
    {
        // App Store builds are re-signed by Apple, so the certificate only names the store.
        InstalledApp? app = Parse(
            info: Info(("NSHumanReadableCopyright", "© 2024 Acme Corporation. All rights reserved.")),
            signingPublisher: "Mac App Store");

        Assert.Equal("Acme Corporation", app!.Publisher);
    }

    [Fact]
    public void Parse_Uses_The_Copyright_For_An_Unsigned_Bundle()
    {
        InstalledApp? app = Parse(
            info: Info(("NSHumanReadableCopyright", "Copyright 2024 OpenEmu Team")),
            signingPublisher: null);

        Assert.Equal("OpenEmu Team", app!.Publisher);
    }

    [Fact]
    public void Parse_Uses_The_Localized_Copyright_When_Info_Plist_Has_None()
    {
        InstalledApp? app = Parse(
            localizedInfo: Info(("NSHumanReadableCopyright", "Copyright © 2004–2023 Apple Inc. All rights reserved.")),
            signingPublisher: "Mac App Store");

        Assert.Equal("Apple Inc.", app!.Publisher);
    }

    [Fact]
    public void Parse_Uses_The_Localized_Display_Name_Like_Finder()
    {
        InstalledApp? app = Parse(
            localizedInfo: Info(("CFBundleDisplayName", "Find My")),
            bundlePath: "/System/Applications/FindMy.app");

        Assert.Equal("Find My", app!.Name);
    }
}
