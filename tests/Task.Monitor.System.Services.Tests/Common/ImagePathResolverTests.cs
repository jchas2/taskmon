using System.Runtime.Versioning;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Tests.Common;

// Service and driver image paths use Windows semantics (drive letters, '\' separators) that Path
// only applies on Windows, so the path-resolution tests run there only.
public sealed class ImagePathResolverTests
{
    private const string WindowsDir = @"C:\Windows";

    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Expand_Expands_Environment_Variables_In_A_Service_Path()
    {
        Environment.SetEnvironmentVariable("TASKMON_TEST_ROOT", @"C:\Windows");

        string? result = ImagePathResolver.Expand(@"%TASKMON_TEST_ROOT%\System32\svchost.exe -k netsvcs", WindowsDir);

        Assert.Equal(@"C:\Windows\System32\svchost.exe -k netsvcs", result);
    }

    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Expand_Leaves_An_Already_Rooted_Path_Unchanged()
    {
        string? result = ImagePathResolver.Expand(@"D:\Custom\xyz.sys", WindowsDir);

        Assert.Equal(@"D:\Custom\xyz.sys", result);
    }

    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Expand_Leaves_A_Quoted_Command_Line_Unchanged()
    {
        const string command = @"""C:\Program Files\Acme\acme.exe"" --service";

        Assert.Equal(command, ImagePathResolver.Expand(command, WindowsDir));
    }

    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Expand_Resolves_A_SystemRoot_Relative_Path()
    {
        string? result = ImagePathResolver.Expand(@"\SystemRoot\System32\drivers\xyz.sys", WindowsDir);

        Assert.Equal(@"C:\Windows\System32\drivers\xyz.sys", result);
    }

    [Fact]
    public void Expand_Returns_Null_For_A_Null_Or_Empty_Path()
    {
        Assert.Null(ImagePathResolver.Expand(null, WindowsDir));
        Assert.Null(ImagePathResolver.Expand(string.Empty, WindowsDir));
        Assert.Null(ImagePathResolver.Expand("   ", WindowsDir));
    }

    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Expand_Strips_The_NT_Device_Path_Prefix()
    {
        string? result = ImagePathResolver.Expand(@"\??\C:\Windows\System32\drivers\xyz.sys", WindowsDir);

        Assert.Equal(@"C:\Windows\System32\drivers\xyz.sys", result);
    }

    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Expand_Treats_A_Bare_Filename_As_Living_Under_System32_Drivers()
    {
        string? result = ImagePathResolver.Expand("xyz.sys", WindowsDir);

        Assert.Equal(@"C:\Windows\System32\drivers\xyz.sys", result);
    }

    // A handful of real drivers (Acx01000, ahcache, seen on a live machine) register with this
    // exact form - no leading backslash, but already carrying their own directory component. It
    // must not be mistaken for the bare-filename case above and get System32\drivers doubled up.
    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void Expand_Treats_A_Relative_Path_With_Its_Own_Directory_As_Relative_To_Windows_Directory()
    {
        string? result = ImagePathResolver.Expand(@"system32\drivers\xyz.sys", WindowsDir);

        Assert.Equal(@"C:\Windows\system32\drivers\xyz.sys", result);
    }
}
