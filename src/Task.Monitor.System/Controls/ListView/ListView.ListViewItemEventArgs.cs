namespace Task.Monitor.System.Controls.ListView;

public sealed class ListViewItemEventArgs(ListViewItem item) : EventArgs
{
    public ListViewItem Item { get; } = item;
}