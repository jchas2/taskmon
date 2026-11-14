namespace Task.Monitor.Gui.Controls.Summary2.Layout;

public sealed class LayoutNode<T> where T : class
{
    public required int Id          { get; init; }
    public bool         IsSplit     { get; set; }
    public Orientation  Orientation { get; set; }
    public float        Ratio       { get; set; } = 0.5f;
    public int          FirstId     { get; set; }
    public int          SecondId    { get; set; }
    // What a pane holds, null on a split.
    public T?           Pane        { get; set; }
}
