using System.Drawing;
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
        Color backgroundColor,
        Color foregroundColor)
        : base(
            text,
            backgroundColor,
            foregroundColor) => this.AssociatedControl = associatedControl;
    
    public Control AssociatedControl { get; }
    public Action? LoadItems { get; init; }
}
