namespace Task.Monitor.Cli.Utils.Tests;

public sealed class ListTExtensionTests
{
    [Fact]
    public void Should_Add_Item_If_Missing()
    {
        List<int> list = new();
        list.AddIfMissing(1);
        list.AddIfMissing(2);
        
        Assert.Equal(2, list.Count);
        Assert.Contains(1, list);
        Assert.Contains(2, list);
    }

    [Fact]
    public void Should_Not_Add_Duplicates()
    {
        List<string> list = new();
        list.AddIfMissing("abc");
        list.AddIfMissing("abc");
        list.AddIfMissing("def");
        
        Assert.Equal(2, list.Count);
    }
}