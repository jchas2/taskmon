namespace Task.Monitor.System.Controls.ListView;

public class ListViewColumnHeader
{
    private string text;
    private const int DefaultColumnWidth = 30;
    
    public ListViewColumnHeader(string text) =>
        this.text = text;

    public ListViewColumnHeader(
        string text,
        ConsoleColor backgroundColor,
        ConsoleColor foregroundColor)
        : this(text)
    {
        BackgroundColour = backgroundColor;
        ForegroundColour = foregroundColor;
    }

    public ConsoleColor? BackgroundColour { get; set; }
    public ConsoleColor? ForegroundColour { get; set; }
    public bool RightAligned { get; set; } = false;
    
    public string Text
    {
        get => text;
        set => text = value ?? throw new ArgumentNullException(nameof(value));
    }
    
    public int Width { get; set; } = DefaultColumnWidth;
}
