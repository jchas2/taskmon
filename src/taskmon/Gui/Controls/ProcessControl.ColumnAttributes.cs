namespace Task.Monitor.Gui.Controls;

public partial class ProcessControl
{
    public class ColumnPropertyAttribute(string property) : Attribute
    {
        public string Property { get; } = property;
    }

    public class ColumnSortKeyAttribute(ConsoleKey key) : Attribute
    {
        public ConsoleKey Key { get; } = key;
    }

    public class ColumnTitleAttribute(string title) : Attribute
    {
        public string Title { get; } = title;
    }
}