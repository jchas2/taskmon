using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;

namespace Task.Monitor.Gui.Controls;

public class MenuListViewItem : ListViewItem
{
    public MenuListViewItem(Control associatedControl, string text)
        : base(text) => this.AssociatedControl = associatedControl;
    
    public MenuListViewItem(
        Control associatedControl, 
        string text,
        ConsoleColor backgroundColor,
        ConsoleColor foregroundColor) 
        : base(
            text,
            backgroundColor,
            foregroundColor) => this.AssociatedControl = associatedControl;
    
    public Control AssociatedControl { get; }
}
