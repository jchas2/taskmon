namespace Task.Monitor.Gui.Controls.Summary2.Layout;

// Row = children laid out side by side (a vertical divider between them); Column = children
// stacked (a horizontal divider). Named after the CSS/Flexbox convention - a "row" lays its
// children out along a horizontal axis - since that's a more broadly recognised convention than
// inventing new terms for the same idea.
public enum Orientation
{
    Row,
    Column,
}
