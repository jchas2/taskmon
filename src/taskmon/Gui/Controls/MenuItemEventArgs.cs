namespace Task.Monitor.Gui.Controls;

public sealed class MenuItemEventArgs(MenuListViewItem item) : EventArgs
{
    public MenuListViewItem? Item { get; } = item;
}
