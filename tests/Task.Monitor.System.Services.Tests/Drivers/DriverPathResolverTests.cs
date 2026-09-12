using Task.Monitor.System.Services.Drivers;

namespace Task.Monitor.System.Services.Tests.Drivers;

public sealed class DriverPathResolverTests
{
    private const string WindowsDir = @"C:\Windows";

    [Fact]
    public void Expand_Returns_Null_For_A_Null_Or_Empty_Path()
    {
        Assert.Null(DriverPathResolver.Expand(null, WindowsDir));
        Assert.Null(DriverPathResolver.Expand(string.Empty, WindowsDir));
        Assert.Null(DriverPathResolver.Expand("   ", WindowsDir));
    }

    [Fact]
    public void Expand_Resolves_A_SystemRoot_Relative_Path()
    {
        string? result = DriverPathResolver.Expand(@"\SystemRoot\System32\drivers\xyz.sys", WindowsDir);

        Assert.Equal(@"C:\Windows\System32\drivers\xyz.sys", result);
    }

    [Fact]
    public void Expand_Strips_The_NT_Device_Path_Prefix()
    {
        string? result = DriverPathResolver.Expand(@"\??\C:\Windows\System32\drivers\xyz.sys", WindowsDir);

        Assert.Equal(@"C:\Windows\System32\drivers\xyz.sys", result);
    }

    [Fact]
    public void Expand_Leaves_An_Already_Rooted_Path_Unchanged()
    {
        string? result = DriverPathResolver.Expand(@"D:\Custom\xyz.sys", WindowsDir);

        Assert.Equal(@"D:\Custom\xyz.sys", result);
    }

    [Fact]
    public void Expand_Treats_A_Bare_Filename_As_Living_Under_System32_Drivers()
    {
        string? result = DriverPathResolver.Expand("xyz.sys", WindowsDir);

        Assert.Equal(@"C:\Windows\System32\drivers\xyz.sys", result);
    }

    // A handful of real drivers (Acx01000, ahcache, seen on a live machine) register with this
    // exact form - no leading backslash, but already carrying their own directory component. It
    // must not be mistaken for the bare-filename case above and get System32\drivers doubled up.
    [Fact]
    public void Expand_Treats_A_Relative_Path_With_Its_Own_Directory_As_Relative_To_Windows_Directory()
    {
        string? result = DriverPathResolver.Expand(@"system32\drivers\xyz.sys", WindowsDir);

        Assert.Equal(@"C:\Windows\system32\drivers\xyz.sys", result);
    }
}
