using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class StartupCommandLineTests
{
    [Fact]
    public void Splits_A_Quoted_Path_From_Its_Arguments()
    {
        (string path, string arguments) =
            StartupCommandLine.Split("\"C:\\Program Files\\App\\app.exe\" --start /min");

        Assert.Equal(@"C:\Program Files\App\app.exe", path);
        Assert.Equal("--start /min", arguments);
    }

    [Fact]
    public void Splits_A_Quoted_Path_With_No_Arguments()
    {
        (string path, string arguments) = StartupCommandLine.Split("\"C:\\Tools\\agent.exe\"");

        Assert.Equal(@"C:\Tools\agent.exe", path);
        Assert.Equal("", arguments);
    }

    [Fact]
    public void Keeps_A_Single_Unquoted_Token_As_The_Path()
    {
        (string path, string arguments) = StartupCommandLine.Split("OneDrive.exe");

        Assert.Equal("OneDrive.exe", path);
        Assert.Equal("", arguments);
    }

    [Fact]
    public void Splits_An_Unquoted_Spaceless_Path_From_Its_Arguments()
    {
        (string path, string arguments) =
            StartupCommandLine.Split(@"C:\Windows\System32\rundll32.exe shell32.dll,Control_RunDLL");

        Assert.Equal(@"C:\Windows\System32\rundll32.exe", path);
        Assert.Equal("shell32.dll,Control_RunDLL", arguments);
    }

    [Fact]
    public void Expands_Environment_Variables()
    {
        (string path, _) = StartupCommandLine.Split(@"%SystemRoot%\System32\notepad.exe");

        Assert.Equal(
            Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\notepad.exe"),
            path);
        Assert.DoesNotContain("%", path);
    }

    [Fact]
    public void Probes_For_An_Existing_Prefix_When_An_Unquoted_Path_Contains_Spaces()
    {
        string directory = Path.Combine(Path.GetTempPath(), "taskmon startup test");
        string executable = Path.Combine(directory, "my agent.exe");

        Directory.CreateDirectory(directory);
        File.WriteAllText(executable, string.Empty);

        try {
            (string path, string arguments) = StartupCommandLine.Split($"{executable} --run now");

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
    public void Returns_Empty_For_A_Blank_Command(string? command)
    {
        (string path, string arguments) = StartupCommandLine.Split(command);

        Assert.Equal("", path);
        Assert.Equal("", arguments);
    }

    [Fact]
    public void Handles_An_Unbalanced_Quote()
    {
        (string path, string arguments) = StartupCommandLine.Split("\"C:\\x.exe");

        Assert.Equal(@"C:\x.exe", path);
        Assert.Equal("", arguments);
    }
}
