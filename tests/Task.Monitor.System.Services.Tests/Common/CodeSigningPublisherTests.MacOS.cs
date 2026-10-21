#if __APPLE__
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Tests.Common;

public sealed class CodeSigningPublisherTests
{
    [Fact]
    public void Keeps_A_Summary_With_No_Certificate_Type_Prefix()
    {
        Assert.Equal("Some Vendor", CodeSigningPublisher.Parse("Some Vendor"));
    }

    [Fact]
    public void Maps_Apple_Platform_Signing_To_Apple()
    {
        Assert.Equal("Apple Inc.", CodeSigningPublisher.Parse("Software Signing"));
    }

    [Fact]
    public void Maps_App_Store_Re_Signing_To_The_Store()
    {
        Assert.Equal("Mac App Store", CodeSigningPublisher.Parse("Apple Mac OS Application Signing"));
    }

    [Theory]
    [InlineData("/usr/local/bin/tool")]
    [InlineData("/Library/Application Support/Vendor/tool")]
    public void Resolve_Does_Not_Treat_Writable_Locations_As_Apples(string path)
    {
        Assert.Null(CodeSigningPublisher.Resolve(path));
    }

    [Fact]
    public void Resolve_Reads_A_Signed_Programs_Publisher()
    {
        Assert.Equal("Apple Inc.", CodeSigningPublisher.Resolve("/bin/ls"));
    }

    [Fact]
    public void Resolve_Returns_Null_For_Unsigned_Code_Outside_The_System_Volume()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sh");
        File.WriteAllText(path, "#!/bin/sh\n");

        try {
            Assert.Null(CodeSigningPublisher.Resolve(path));
        }
        finally {
            File.Delete(path);
        }
    }

    // Apple's kext bundles hold no code, so they carry no signature; the sealed volume vouches for them.
    [SkippableFact]
    public void Resolve_Treats_Unsigned_Code_On_The_System_Volume_As_Apples()
    {
        const string UsbHostFamily = "/System/Library/Extensions/IOUSBHostFamily.kext";

        Skip.IfNot(Directory.Exists(UsbHostFamily), $"{UsbHostFamily} is not installed");

        Assert.Equal("Apple Inc.", CodeSigningPublisher.Resolve(UsbHostFamily));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Returns_Null_For_Unsigned_Code(string? summary)
    {
        Assert.Null(CodeSigningPublisher.Parse(summary));
    }

    [Theory]
    [InlineData("Developer ID Application: Microsoft Corporation (UBF8T346G9)", "Microsoft Corporation")]
    [InlineData("Developer ID Application: Denver Technologies, Inc (2BBY89MBSN)", "Denver Technologies, Inc")]
    [InlineData("Apple Development: Jane Appleseed (A1B2C3D4E5)", "Jane Appleseed")]
    [InlineData("Developer ID Application: No Team Suffix", "No Team Suffix")]
    public void Takes_The_Name_From_A_Developer_Certificate(string summary, string expected)
    {
        Assert.Equal(expected, CodeSigningPublisher.Parse(summary));
    }
}
#endif
