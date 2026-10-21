#if __WIN32__
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed partial class StartupServiceTests
{
    [Fact]
    public void SplitCommandLine_Expands_Environment_Variables()
    {
        (string path, _) = StartupService.SplitCommandLine(@"%SystemRoot%\System32\notepad.exe");

        Assert.Equal(
            Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\notepad.exe"),
            path);
        Assert.DoesNotContain("%", path);
    }

    [Fact]
    public void SplitCommandLine_Handles_An_Unbalanced_Quote()
    {
        (string path, string arguments) = StartupService.SplitCommandLine("\"C:\\x.exe");

        Assert.Equal(@"C:\x.exe", path);
        Assert.Equal("", arguments);
    }

    [Fact]
    public void SplitCommandLine_Keeps_A_Single_Unquoted_Token_As_The_Path()
    {
        (string path, string arguments) = StartupService.SplitCommandLine("OneDrive.exe");

        Assert.Equal("OneDrive.exe", path);
        Assert.Equal("", arguments);
    }

    [Fact]
    public void SplitCommandLine_Probes_For_An_Existing_Prefix_When_An_Unquoted_Path_Contains_Spaces()
    {
        string directory = Path.Combine(Path.GetTempPath(), "taskmon startup test");
        string executable = Path.Combine(directory, "my agent.exe");

        Directory.CreateDirectory(directory);
        File.WriteAllText(executable, string.Empty);

        try {
            (string path, string arguments) = StartupService.SplitCommandLine($"{executable} --run now");

            Assert.Equal(executable, path);
            Assert.Equal("--run now", arguments);
        }
        finally {
            File.Delete(executable);
            Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SplitCommandLine_Returns_Empty_For_A_Blank_Command(string? command)
    {
        (string path, string arguments) = StartupService.SplitCommandLine(command);

        Assert.Equal("", path);
        Assert.Equal("", arguments);
    }

    [Fact]
    public void SplitCommandLine_Splits_An_Unquoted_Spaceless_Path_From_Its_Arguments()
    {
        (string path, string arguments) =
            StartupService.SplitCommandLine(@"C:\Windows\System32\rundll32.exe shell32.dll,Control_RunDLL");

        Assert.Equal(@"C:\Windows\System32\rundll32.exe", path);
        Assert.Equal("shell32.dll,Control_RunDLL", arguments);
    }

    [Fact]
    public void SplitCommandLine_Splits_A_Quoted_Path_From_Its_Arguments()
    {
        (string path, string arguments) =
            StartupService.SplitCommandLine("\"C:\\Program Files\\App\\app.exe\" --start /min");

        Assert.Equal(@"C:\Program Files\App\app.exe", path);
        Assert.Equal("--start /min", arguments);
    }

    [Fact]
    public void SplitCommandLine_Splits_A_Quoted_Path_With_No_Arguments()
    {
        (string path, string arguments) = StartupService.SplitCommandLine("\"C:\\Tools\\agent.exe\"");

        Assert.Equal(@"C:\Tools\agent.exe", path);
        Assert.Equal("", arguments);
    }
}
#endif
