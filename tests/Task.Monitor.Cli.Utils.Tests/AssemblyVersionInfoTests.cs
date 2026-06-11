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
}