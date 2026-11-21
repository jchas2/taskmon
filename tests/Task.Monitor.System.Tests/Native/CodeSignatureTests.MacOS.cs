#if __APPLE__
using Task.Monitor.System.Native;

namespace Task.Monitor.System.Tests.Native;

public sealed class CodeSignatureTests
{
    // Every macOS install ships these; Apple's own binaries are signed "Software Signing".
    private const string AppleBinary = "/bin/ls";
    private const string AppleBundle = "/System/Applications/Calculator.app";

    [Fact]
    public void Reads_The_Signing_Certificate_Of_An_Apple_Binary()
    {
        Assert.Equal("Software Signing", CodeSignature.GetSigningCertificateSummary(AppleBinary));
    }

    [SkippableFact]
    public void Reads_The_Signing_Certificate_Of_An_App_Bundle()
    {
        Skip.IfNot(Directory.Exists(AppleBundle), $"{AppleBundle} is not installed");

        Assert.Equal("Software Signing", CodeSignature.GetSigningCertificateSummary(AppleBundle));
    }

    [Fact]
    public void Returns_Null_For_A_Path_That_Does_Not_Exist()
    {
        Assert.Null(CodeSignature.GetSigningCertificateSummary(
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "missing")));
    }

    [Fact]
    public void Returns_Null_For_Unsigned_Code()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sh");
        File.WriteAllText(path, "#!/bin/sh\necho unsigned\n");

        try {
            Assert.Null(CodeSignature.GetSigningCertificateSummary(path));
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void Survives_Repeated_Calls()
    {
        // An over-release (wrapping a borrowed reference) tends to crash on reuse, not first use.
        for (int i = 0; i < 200; i++) {
            Assert.Equal("Software Signing", CodeSignature.GetSigningCertificateSummary(AppleBinary));
        }
    }
}
#endif
