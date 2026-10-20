#if __APPLE__
using Task.Monitor.System.Services.MacOS;

namespace Task.Monitor.System.Services.Tests.MacOS;

public sealed class AppBundleTests : IDisposable
{
    private readonly string root;

    public AppBundleTests()
    {
        root = Directory.CreateTempSubdirectory("taskmon-apps-").FullName;
    }

    private string CreateBundle(string relativePath, string? infoPlist = null)
    {
        string bundle = Path.Combine(root, relativePath);
        string contents = Directory.CreateDirectory(Path.Combine(bundle, "Contents")).FullName;

        if (infoPlist is not null) {
            File.WriteAllText(Path.Combine(contents, "Info.plist"), infoPlist);
        }

        return bundle;
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void EnumerateApplications_Finds_Bundles_One_Folder_Down_Without_Entering_Bundles()
    {
        string top     = CreateBundle("Top.app");
        string nested  = CreateBundle(Path.Combine("Utilities", "Nested.app"));
        CreateBundle(Path.Combine("Top.app", "Contents", "Library", "LoginItems", "Helper.app"));
        CreateBundle(Path.Combine("Vendor", "Deeper", "TooDeep.app"));

        List<string> found = AppBundle.EnumerateApplications([root]).ToList();

        Assert.Equal(new[] { nested, top }.Order(StringComparer.Ordinal), found.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EnumerateApplications_Skips_A_Missing_Root()
    {
        Assert.Empty(AppBundle.EnumerateApplications([Path.Combine(root, "missing")]));
    }

    private static string Plist(string body) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0"><dict>{body}</dict></plist>
        """;

    [Fact]
    public void ReadInfo_Converts_Only_The_Requested_Keys()
    {
        string bundle = CreateBundle("Acme.app", Plist(
            "<key>CFBundleShortVersionString</key><string>1.2.3</string>" +
            "<key>CFBundleIdentifier</key><string>com.acme.app</string>"));

        IReadOnlyDictionary<string, object?>? info = AppBundle.ReadInfo(bundle, ["CFBundleIdentifier"]);

        Assert.Equal(["CFBundleIdentifier"], info!.Keys);
    }

    [Fact]
    public void ReadInfo_Reads_The_Info_Plist()
    {
        string bundle = CreateBundle("Acme.app", Plist("<key>CFBundleShortVersionString</key><string>1.2.3</string>"));

        Assert.Equal("1.2.3", AppBundle.ReadInfo(bundle, ["CFBundleShortVersionString"])?.GetValueOrDefault("CFBundleShortVersionString"));
    }

    [Fact]
    public void ReadInfo_Returns_Null_Without_An_Info_Plist()
    {
        Assert.Null(AppBundle.ReadInfo(CreateBundle("Empty.app"), ["CFBundleShortVersionString"]));
    }

    [Fact]
    public void ReadLocalizedInfo_Prefers_The_Loctable()
    {
        // Apple's own apps ship one InfoPlist.loctable keyed by language instead of .lproj folders.
        string bundle    = CreateBundle("FindMy.app", Plist(string.Empty));
        string resources = Directory.CreateDirectory(Path.Combine(bundle, "Contents", "Resources")).FullName;

        File.WriteAllText(
            Path.Combine(resources, "InfoPlist.loctable"),
            Plist("<key>en</key><dict><key>CFBundleDisplayName</key><string>Find My</string></dict>"));

        Assert.Equal("Find My", AppBundle.ReadLocalizedInfo(bundle, ["CFBundleDisplayName"])?.GetValueOrDefault("CFBundleDisplayName"));
    }

    [Fact]
    public void ReadLocalizedInfo_Reads_The_English_Strings()
    {
        string bundle = CreateBundle("GarageBand.app", Plist(string.Empty));
        string lproj  = Directory.CreateDirectory(Path.Combine(bundle, "Contents", "Resources", "en.lproj")).FullName;

        File.WriteAllText(
            Path.Combine(lproj, "InfoPlist.strings"),
            "\"NSHumanReadableCopyright\" = \"Copyright © 2004–2023 Apple Inc. All rights reserved.\";");

        Assert.Equal(
            "Copyright © 2004–2023 Apple Inc. All rights reserved.",
            AppBundle.ReadLocalizedInfo(bundle, ["NSHumanReadableCopyright"])?.GetValueOrDefault("NSHumanReadableCopyright"));
    }

    [Fact]
    public void ReadLocalizedInfo_Returns_Null_For_An_Unlocalized_Bundle()
    {
        Assert.Null(AppBundle.ReadLocalizedInfo(CreateBundle("Plain.app", Plist(string.Empty)), ["CFBundleDisplayName"]));
    }
}
#endif
