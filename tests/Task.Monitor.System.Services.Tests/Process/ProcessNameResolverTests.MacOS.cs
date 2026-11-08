#if __APPLE__
using Task.Monitor.System.Services.Process;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed class ProcessNameResolverTests : IDisposable
{
    private readonly string tempRoot = Directory.CreateTempSubdirectory("taskmon-names-").FullName;

    [Theory]
    [InlineData("/Applications/Warp.app/Contents/MacOS/stable", "/Applications/Warp.app")]
    [InlineData(
        "/Applications/Brave Browser.app/Contents/Frameworks/Brave.framework/Helpers/Brave Browser Helper.app/Contents/MacOS/Brave Browser Helper",
        "/Applications/Brave Browser.app/Contents/Frameworks/Brave.framework/Helpers/Brave Browser Helper.app")]
    public void BundlePathFor_Finds_The_App_That_Holds_The_Executable(string executablePath, string expected) =>
        Assert.Equal(expected, ProcessNameResolver.BundlePathFor(executablePath));

    [Theory]
    [InlineData("")]
    [InlineData("/usr/bin/true")]
    [InlineData("/Applications/Foo.app/Contents/Helpers/foo-helper")]
    [InlineData("/System/Library/Frameworks/Security.framework/Versions/A/XPCServices/authd.xpc/Contents/MacOS/authd")]
    public void BundlePathFor_Ignores_An_Executable_Outside_An_App_Bundle(string executablePath) =>
        Assert.Null(ProcessNameResolver.BundlePathFor(executablePath));

    [Fact]
    public void ChooseName_Falls_Back_To_The_Kernel_Name_Without_A_Path() =>
        Assert.Equal("kernel_task", ProcessNameResolver.ChooseName("", null, null, "kernel_task"));

    [Fact]
    public void ChooseName_Prefers_The_Bundle_Name()
    {
        const string executablePath = "/Applications/Warp.app/Contents/MacOS/stable";

        Assert.Equal("Warp", ProcessNameResolver.ChooseName(executablePath, "Warp", executablePath, "stable"));
    }

    [Theory]
    [InlineData("/System/Library/CoreServices/AccessibilitySettingsExtension")]
    [InlineData(null)]
    public void ChooseName_Uses_The_Full_File_Name_The_Kernel_Cuts_Short(string? launchPath)
    {
        const string executablePath = "/System/Library/CoreServices/AccessibilitySettingsExtension";

        Assert.Equal(
            "AccessibilitySettingsExtension",
            ProcessNameResolver.ChooseName(executablePath, null, launchPath, "AccessibilitySet"));
    }

    [Fact]
    public void ChooseName_Uses_The_Symlink_A_Process_Was_Launched_By() =>
        Assert.Equal(
            "claude",
            ProcessNameResolver.ChooseName(
                "/Users/someone/.local/share/claude/versions/2.1.311", null, "/Users/someone/.local/bin/claude", "2.1.311"));

    private string CreateBundle(string folderName, string? mainExecutable)
    {
        string bundlePath = Path.Combine(tempRoot, folderName);

        Directory.CreateDirectory(Path.Combine(bundlePath, "Contents", "MacOS"));

        if (mainExecutable is not null) {
            File.WriteAllText(
                Path.Combine(bundlePath, "Contents", "Info.plist"),
                $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0"><dict>
                <key>CFBundleExecutable</key><string>{mainExecutable}</string>
                <key>CFBundleName</key><string>Not The Folder Name</string>
                </dict></plist>
                """);
        }

        return bundlePath;
    }

    public void Dispose() => Directory.Delete(tempRoot, recursive: true);

    [Fact]
    public void FindBundleName_Ignores_A_Bundle_With_No_Info_Plist()
    {
        string bundlePath = CreateBundle("Warp.app", mainExecutable: null);

        Assert.Null(new ProcessNameResolver().FindBundleName(Path.Combine(bundlePath, "Contents", "MacOS", "stable")));
    }

    [Fact]
    public void FindBundleName_Leaves_Other_Executables_In_The_Bundle_Alone()
    {
        string bundlePath = CreateBundle("Warp.app", "stable");

        Assert.Null(new ProcessNameResolver().FindBundleName(Path.Combine(bundlePath, "Contents", "MacOS", "crashpad_handler")));
    }

    [Fact]
    public void FindBundleName_Names_The_Main_Executable_After_The_App_Folder()
    {
        string bundlePath = CreateBundle("Warp.app", "stable");

        Assert.Equal("Warp", new ProcessNameResolver().FindBundleName(Path.Combine(bundlePath, "Contents", "MacOS", "stable")));
    }

    [Fact]
    public void Resolve_Names_A_Process_After_The_Symlink_It_Was_Launched_By()
    {
        string link = Path.Combine(tempRoot, "taskmon-launch-name");
        File.CreateSymbolicLink(link, "/bin/sleep");

        using SysDiag::Process child = SysDiag::Process.Start(link, "30");

        try {
            Assert.Equal("taskmon-launch-name", new ProcessNameResolver().Resolve(child.Id, 0, "/bin/sleep", "sleep"));
        }
        finally {
            child.Kill();
            child.WaitForExit();
        }
    }

    // No such pid, so there is no launch path and the name comes from the executable path alone.
    [Fact]
    public void Resolve_Starts_Again_When_The_Process_Runs_A_New_Executable()
    {
        ProcessNameResolver resolver = new();

        Assert.Equal("true", resolver.Resolve(int.MaxValue, 0, "/usr/bin/true", "true"));
        Assert.Equal("false", resolver.Resolve(int.MaxValue, 0, "/usr/bin/false", "false"));
    }
}
#endif
