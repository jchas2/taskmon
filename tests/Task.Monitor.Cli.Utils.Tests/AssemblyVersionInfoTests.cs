namespace Task.Monitor.Cli.Utils.Tests;

public sealed class AssemblyVersionInfoTests
{
    [Fact]
    public void Should_Get_VersionInfo()
    {
        string version = AssemblyVersionInfo.GetVersion();
        Assert.NotNull(version);
        Assert.NotEqual("0.0.0.0", version);
    }

    [Fact]
    public void The_Short_Version_Is_The_Major_And_Minor_Parts_Of_The_Full_Version()
    {
        string[] parts = AssemblyVersionInfo.GetVersion().Split('.');

        Assert.Equal(4, parts.Length);
        Assert.Equal($"{parts[0]}.{parts[1]}", AssemblyVersionInfo.GetShortVersion());
    }
}
