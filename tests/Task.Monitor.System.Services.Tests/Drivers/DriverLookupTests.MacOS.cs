#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Drivers;

namespace Task.Monitor.System.Services.Tests.Drivers;

public sealed partial class DriverLookupTests
{
    private const string BundlePath = "/Library/Extensions/Example.kext";

    // Every Apple Silicon Mac has USB, so this family is installed and loaded.
    private const string UsbHostFamily = "com.apple.iokit.IOUSBHostFamily";

    [Fact]
    public void GetDrivers_Finds_The_USB_Host_Family_Loaded_With_A_Version_And_Apple_Publisher()
    {
        DriverInfo driver = Assert.Single(DriverLookup.GetDrivers(), driver => driver.Name == UsbHostFamily);

        Assert.Equal(DriverStatus.Loaded, driver.Status);
        Assert.False(string.IsNullOrWhiteSpace(driver.Version));
        Assert.Equal("Apple Inc.", driver.Publisher);
    }

    [Fact]
    public void GetDrivers_Includes_Kexts_Bundled_In_Another_Kexts_PlugIns()
    {
        Assert.Contains(DriverLookup.GetDrivers(), driver => driver.ImagePath?.Contains("/Contents/PlugIns/") == true);
    }

    [Fact]
    public void GetDrivers_Leaves_Out_Kernel_Interfaces()
    {
        Assert.DoesNotContain(DriverLookup.GetDrivers(), driver => driver.Name.StartsWith("com.apple.kpi.", StringComparison.Ordinal));
    }

    // The kernel's page table and trusted execution monitors both report the same bundle path, which
    // doesn't exist on disk; neither may hide the other.
    [SkippableFact]
    public void GetDrivers_Lists_Every_Kext_Loaded_From_Outside_The_Extensions_Folders()
    {
        Dictionary<string, object?> loaded = IOKit.GetLoadedKextInfo(["OSBundlePath"]) ?? new();

        string[] outside = loaded
            .Where(entry => entry.Value is IReadOnlyDictionary<string, object?> info &&
                info.GetValueOrDefault("OSBundlePath") is string path &&
                !path.StartsWith("/System/Library/Extensions/", StringComparison.Ordinal) &&
                !path.StartsWith("/Library/Extensions/", StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .ToArray();

        Skip.If(outside.Length == 0, "No kexts are loaded from outside the Extensions folders");

        List<DriverInfo> drivers = DriverLookup.GetDrivers();

        foreach (string bundleId in outside) {
            Assert.Contains(drivers, driver => driver.Name == bundleId);
        }
    }

    private static Dictionary<string, object?> Info(
        string? bundleId = "com.example.driver",
        string? name     = "Example Driver",
        string? version  = "1.0",
        string? required = null) =>
        new() {
            ["CFBundleIdentifier"] = bundleId,
            ["CFBundleName"]       = name,
            ["CFBundleVersion"]    = version,
            ["OSBundleRequired"]   = required
        };

    private static Dictionary<string, object?> Loaded(string bundleId, string? version = "1.0", string? path = BundlePath) =>
        new() {
            [bundleId] = new Dictionary<string, object?> {
                ["CFBundleVersion"] = version,
                ["OSBundlePath"]    = path
            }
        };

    [Fact]
    public void MapKext_Falls_Back_To_The_Bundle_Id_Without_A_Name()
    {
        DriverInfo? driver = DriverLookup.MapKext(BundlePath, Info(name: null), new Dictionary<string, object?>());

        Assert.Equal("com.example.driver", driver?.DisplayName);
    }

    [Fact]
    public void MapKext_Marks_An_Unloaded_Kext_With_Its_Installed_Version()
    {
        DriverInfo? driver = DriverLookup.MapKext(BundlePath, Info(version: "2.0"), new Dictionary<string, object?>());

        Assert.NotNull(driver);
        Assert.Equal("com.example.driver", driver.Name);
        Assert.Equal("Example Driver", driver.DisplayName);
        Assert.Equal(BundlePath, driver.ImagePath);
        Assert.Equal(DriverStatus.NotLoaded, driver.Status);
        Assert.Equal("2.0", driver.Version);
    }

    [Fact]
    public void MapKext_Marks_A_Kext_Loaded_From_Another_Path_As_Not_Loaded()
    {
        DriverInfo? driver = DriverLookup.MapKext(
            BundlePath, Info(), Loaded("com.example.driver", path: "/Library/Other/Example.kext"));

        Assert.Equal(DriverStatus.NotLoaded, driver?.Status);
    }

    [Fact]
    public void MapKext_Marks_A_Loaded_Kext_And_Shows_Its_Loaded_Version()
    {
        DriverInfo? driver = DriverLookup.MapKext(BundlePath, Info(version: "2.0"), Loaded("com.example.driver", version: "1.0"));

        Assert.NotNull(driver);
        Assert.Equal(DriverStatus.Loaded, driver.Status);
        Assert.Equal("1.0", driver.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MapKext_Skips_A_Bundle_With_No_Identifier(string? bundleId)
    {
        Assert.Null(DriverLookup.MapKext(BundlePath, Info(bundleId: bundleId), new Dictionary<string, object?>()));
    }

    [Fact]
    public void MapKext_Skips_A_Kernel_Interface()
    {
        Assert.Null(DriverLookup.MapKext(BundlePath, Info(bundleId: "com.apple.kpi.bsd"), new Dictionary<string, object?>()));
    }

    [Fact]
    public void MapKext_Skips_A_Missing_Info_Plist()
    {
        Assert.Null(DriverLookup.MapKext(BundlePath, null, new Dictionary<string, object?>()));
    }

    [Theory]
    [InlineData("\"AMD Framebuffer\"")]
    [InlineData(" AMD Framebuffer ")]
    public void MapKext_Strips_Quotes_And_Spaces_Around_The_Name(string name)
    {
        DriverInfo? driver = DriverLookup.MapKext(BundlePath, Info(name: name), new Dictionary<string, object?>());

        Assert.Equal("AMD Framebuffer", driver?.DisplayName);
    }

    [Theory]
    [InlineData("Root",         DriverStartType.BootStart)]
    [InlineData("Local-Root",   DriverStartType.BootStart)]
    [InlineData("Network-Root", DriverStartType.BootStart)]
    [InlineData("Console",      DriverStartType.SystemStart)]
    [InlineData("Safe Boot",    DriverStartType.SystemStart)]
    [InlineData(null,           DriverStartType.OnDemand)]
    [InlineData("Unexpected",   DriverStartType.OnDemand)]
    public void MapRequired_Maps_The_Boot_Stage_To_A_Start_Type(string? required, DriverStartType expected)
    {
        Assert.Equal(expected, DriverLookup.MapRequired(required));
    }
}
#endif
