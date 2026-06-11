using Task.Monitor.Gui.Controls;

namespace Task.Monitor.Tests.Gui.Controls;

public class ColumnPropertyAttributeTests
{
    [Fact]
    public void Constructor_With_Valid_Property_Initialises_Successfully()
    {
        const string property = "TestProperty";

        ProcessControl.ColumnPropertyAttribute attr = new(property);

        Assert.NotNull(attr);
        Assert.Equal(property, attr.Property);
    }
    
    [Fact]
    public void Is_Attribute()
    {
        ProcessControl.ColumnPropertyAttribute attr = new("test");
        Assert.IsAssignableFrom<Attribute>(attr);
    }
}

public class ColumnSortKeyAttributeTests
{
    [Fact]
    public void Constructor_With_Valid_Key_Initialises_Successfully()
    {
        const ConsoleKey key = ConsoleKey.F1;

        ProcessControl.ColumnSortKeyAttribute attr = new(key);

        Assert.NotNull(attr);
        Assert.Equal(key, attr.Key);
    }
    
    [Theory]
    [InlineData(ConsoleKey.A)]
    [InlineData(ConsoleKey.M)]
    [InlineData(ConsoleKey.P)]
    [InlineData(ConsoleKey.U)]
    [InlineData(ConsoleKey.F1)]
    public void Constructor_With_Various_Keys_Initialises_Correctly(ConsoleKey key)
    {
        ProcessControl.ColumnSortKeyAttribute attr = new(key);
        Assert.Equal(key, attr.Key);
    }

    [Fact]
    public void Is_Attribute()
    {
        ProcessControl.ColumnSortKeyAttribute attr = new(ConsoleKey.A);
        Assert.IsAssignableFrom<Attribute>(attr);
    }
}

public class ColumnTitleAttributeTests
{
    [Fact]
    public void Constructor_With_Valid_Title_Initialises_Successfully()
    {
        const string title = "Process Name";

        ProcessControl.ColumnTitleAttribute attr = new(title);

        Assert.NotNull(attr);
        Assert.Equal(title, attr.Title);
    }
    
    [Theory]
    [InlineData("CPU %")]
    [InlineData("Memory")]
    [InlineData("Disk")]
    [InlineData("PID")]
    public void Constructor_With_Various_Titles_Initialises_Correctly(string title)
    {
        ProcessControl.ColumnTitleAttribute attr = new(title);
        Assert.Equal(title, attr.Title);
    }

    [Fact]
    public void Is_Attribute()
    {
        ProcessControl.ColumnTitleAttribute attr = new("test");
        Assert.IsAssignableFrom<Attribute>(attr);
    }
}
