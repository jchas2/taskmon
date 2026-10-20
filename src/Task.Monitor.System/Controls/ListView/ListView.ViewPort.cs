using System.Drawing;

namespace Task.Monitor.System.Controls.ListView;

public sealed class ViewPort
{
    public Rectangle Bounds          { get; set; }

    public int CurrentPageIndex      { get; set; }

    public int PreviousSelectedIndex { get; set; }

    public void Reset()
    {
        CurrentPageIndex      = 0;
        SelectedIndex         = 0;
        PreviousSelectedIndex = 0;
        RowCount              = 0;
        Bounds                = new Rectangle();
    }

    public int RowCount              { get; set; }

    public int SelectedIndex         { get; set; }
}
