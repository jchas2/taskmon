using System.Diagnostics;

namespace Task.Monitor.Cli.Utils.Tests;

public sealed class QueryableExtensionsTests
{
    /*
    private sealed class Data
    {
        public int Id { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
    }

    List<Data> _data = [
        new Data { Id = 1, Name = "A" },
        new Data { Id = 2, Name = "B" },
        new Data { Id = 3, Name = "C" },
        new Data { Id = 4, Name = "D" }
    ];
    
    [Fact]
    public void Should_Sort_Ascending_By_Id()
    {
        List<Data> sortedData = _data.AsQueryable()
            .DynamicOrderBy("Id", isDescending: false)
            .ToList();
        
        Assert.Equal(1, sortedData[0].Id);
        Assert.Equal(2, sortedData[1].Id);
        Assert.Equal(3, sortedData[2].Id);
        Assert.Equal(4, sortedData[3].Id);
    }
    
    [Fact]
    public void Should_Sort_Ascending_By_Name()
    {
        List<Data> sortedData = _data.AsQueryable()
            .DynamicOrderBy("Name", isDescending: false)
            .ToList();
        
        Assert.Equal("A", sortedData[0].Name);
        Assert.Equal("B", sortedData[1].Name);
        Assert.Equal("C", sortedData[2].Name);
        Assert.Equal("D", sortedData[3].Name);
    }

    [Fact]
    public void Should_Sort_Descending_By_Id()
    {
        List<Data> sortedData = _data.AsQueryable()
            .DynamicOrderBy("Id", isDescending: true)
            .ToList();
        
        Assert.Equal(4, sortedData[0].Id);
        Assert.Equal(3, sortedData[1].Id);
        Assert.Equal(2, sortedData[2].Id);
        Assert.Equal(1, sortedData[3].Id);
        
    }

    [Fact]
    public void Should_Sort_Descending_By_Name()
    {
        List<Data> sortedData = _data.AsQueryable()
            .DynamicOrderBy("Name", isDescending: true)
            .ToList();
        
        Assert.Equal("D", sortedData[0].Name);
        Assert.Equal("C", sortedData[1].Name);
        Assert.Equal("B", sortedData[2].Name);
        Assert.Equal("A", sortedData[3].Name);
    }*/
}