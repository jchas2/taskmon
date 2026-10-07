#if __APPLE__
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class CodeSigningPublisherTests
{
    [Theory]
    [InlineData("Developer ID Application: Microsoft Corporation (UBF8T346G9)", "Microsoft Corporation")]
    [InlineData("Developer ID Application: Denver Technologies, Inc (2BBY89MBSN)", "Denver Technologies, Inc")]
    [InlineData("Apple Development: Jane Appleseed (A1B2C3D4E5)", "Jane Appleseed")]
    [InlineData("Developer ID Application: No Team Suffix", "No Team Suffix")]
    public void Takes_The_Name_From_A_Developer_Certificate(string summary, string expected)
    {
        Assert.Equal(expected, CodeSigningPublisher.Parse(summary));
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

    [Fact]
    public void Keeps_A_Summary_With_No_Certificate_Type_Prefix()
    {
        Assert.Equal("Some Vendor", CodeSigningPublisher.Parse("Some Vendor"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Returns_Null_For_Unsigned_Code(string? summary)
    {
        Assert.Null(CodeSigningPublisher.Parse(summary));
    }
}
#endif
