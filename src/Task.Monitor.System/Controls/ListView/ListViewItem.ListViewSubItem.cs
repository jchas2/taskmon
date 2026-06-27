using System.Drawing;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Controls.ListView;

public class ListViewSubItem
{
    private ListViewItem owner;
    private string text;
    private SubItemStyle? style;

    public ListViewSubItem(ListViewItem owner, string text)
    {
        this.owner = owner;
        this.text = text;
    }

    public ListViewSubItem(
        ListViewItem owner,
        string text,
        Color backgroundColor,
        Color foregroundColor)
    {
        this.owner = owner;
        this.text = text;
        
        style = new SubItemStyle {
            BackgroundColour = backgroundColor,
            ForegroundColour = foregroundColor
        };
    }

    public Color BackgroundColor
    {
        get {
            if (style != null) {
                return style.BackgroundColour;
            }

            return owner.Parent?.BackgroundColour ?? ConsolePalette.Black;
        }
        set {
            style ??= new SubItemStyle();
            
            if (style.BackgroundColour != value) {
                style.BackgroundColour = value;
            }
        }
    }

    internal ListViewItem Owner
    {
        get => owner;
        set => owner = value;
    }
    
    public Color ForegroundColor
    {
        get {
            if (style != null) {
                return style.ForegroundColour;
            }

            return owner.Parent?.ForegroundColour ?? ConsolePalette.White;
        }
        set {
            style ??= new SubItemStyle();
            
            if (style.ForegroundColour != value) {
                style.ForegroundColour = value;
            }
        }
    }

    public string Text
    {
        get => text;
        set => text = value;
    }
}