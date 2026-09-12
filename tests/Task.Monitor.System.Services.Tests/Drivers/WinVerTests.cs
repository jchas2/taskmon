using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Tests.Drivers;

// No dedicated Interop.Win32 test project exists yet (GetCompanyName isn't unit-tested in
// isolation either - only indirectly, through the services/startup lookups that consume it), so
// this lives alongside DriverPathResolverTests as the first direct coverage of WinVer itself.
public sealed class WinVerTests
{
    // kernel32.dll is present with a real version resource on every Windows install, making it a
    // stable target without depending on anything this repo ships.
    private static readonly string Kernel32Path =
        Path.Combine(Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows", "System32", "kernel32.dll");

    [Fact]
    public void GetFileVersion_Returns_A_Four_Part_Version_For_A_Real_File()
    {
        string? version = WinVer.GetFileVersion(Kernel32Path);

        Assert.NotNull(version);
        Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", version);
    }

    [Fact]
    public void GetFileVersion_Returns_Null_For_A_Nonexistent_File()
    {
        string? version = WinVer.GetFileVersion(@"C:\this\path\does\not\exist.sys");

        Assert.Null(version);
    }

    [Fact]
    public void GetFileVersion_Returns_Null_For_A_Null_Or_Empty_Path()
    {
        Assert.Null(WinVer.GetFileVersion(null!));
        Assert.Null(WinVer.GetFileVersion(string.Empty));
    }
}
