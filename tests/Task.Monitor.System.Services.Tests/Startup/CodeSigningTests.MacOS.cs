#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class CodeSigningTests
{
    // Every macOS install ships these; Apple's own binaries are signed "Software Signing".
    private const string AppleBinary = "/bin/ls";
    private const string AppleBundle = "/System/Applications/Calculator.app";

    [Fact]
    public void Reads_The_Signing_Certificate_Of_An_Apple_Binary()
    {
        Assert.Equal("Software Signing", CodeSigning.GetSigningCertificateSummary(AppleBinary));
    }

    [SkippableFact]
    public void Reads_The_Signing_Certificate_Of_An_App_Bundle()
    {
        Skip.IfNot(Directory.Exists(AppleBundle), $"{AppleBundle} is not installed");

        Assert.Equal("Software Signing", CodeSigning.GetSigningCertificateSummary(AppleBundle));
    }

    [Fact]
    public void Returns_Null_For_Unsigned_Code()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sh");
        File.WriteAllText(path, "#!/bin/sh\necho unsigned\n");

        try {
            Assert.Null(CodeSigning.GetSigningCertificateSummary(path));
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void Returns_Null_For_A_Path_That_Does_Not_Exist()
    {
        Assert.Null(CodeSigning.GetSigningCertificateSummary(
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "missing")));
    }

    [Fact]
    public void Survives_Repeated_Calls()
    {
        // An over-release (wrapping a borrowed reference) tends to crash on reuse, not first use.
        for (int i = 0; i < 200; i++) {
            Assert.Equal("Software Signing", CodeSigning.GetSigningCertificateSummary(AppleBinary));
        }
    }
}
#endif
