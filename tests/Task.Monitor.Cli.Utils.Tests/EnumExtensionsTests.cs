using System.ComponentModel;

namespace Task.Monitor.Cli.Utils.Tests;

public sealed class EnumExtensionsTests
{
    private enum TestEnum
    {
        [Description("Value0 Description")]
        Value0 = 0,
        Value1 = 1
    }

    [Fact]
    public void Should_GetDescription()
    {
        var value = TestEnum.Value0;
        string description = value.GetDescription();
        Assert.Equal("Value0 Description", description);
    }

    [Fact]
    public void Should_Not_GetDescription_If_Not_Attributted()
    {
        var value = TestEnum.Value1;
        string description = value.GetDescription();
        Assert.Equal("Value1", description);
    }
}
