#if __WIN32__
using Task.Monitor.System.Native;

namespace Task.Monitor.System.Tests.Native;

public sealed class VersionResourceTests
{
    // kernel32.dll is present with a real version resource on every Windows install, making it a
    // stable target without depending on anything this repo ships.
    private static readonly string Kernel32Path =
        Path.Combine(Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows", "System32", "kernel32.dll");

    [Fact]
    public void GetFileVersion_Returns_A_Four_Part_Version_For_A_Real_File()
    {
        string? version = VersionResource.GetFileVersion(Kernel32Path);

        Assert.NotNull(version);
        Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", version);
    }

    [Fact]
    public void GetFileVersion_Returns_Null_For_A_Nonexistent_File()
    {
        string? version = VersionResource.GetFileVersion(@"C:\this\path\does\not\exist.sys");

        Assert.Null(version);
    }

    [Fact]
    public void GetFileVersion_Returns_Null_For_A_Null_Or_Empty_Path()
    {
        Assert.Null(VersionResource.GetFileVersion(null!));
        Assert.Null(VersionResource.GetFileVersion(string.Empty));
    }
}
#endif
