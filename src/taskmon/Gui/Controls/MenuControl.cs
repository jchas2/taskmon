using System.Drawing;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;

namespace Task.Monitor.Gui.Controls;

public sealed class MenuControl : Control
{
    private readonly ListView menuControl;
    private readonly AppConfig appConfig;

    public event EventHandler<MenuItemEventArgs>? MenuItemClicked;

    public MenuControl(ISystemTerminal terminal, AppConfig appConfig) : base(terminal)
    {
        this.appConfig = appConfig;
        
        menuControl = new ListView(terminal) {
            ShowColumnHeaders = false,
            ShowCheckboxes = false,
            Visible = true
        };
        
        menuControl.ColumnHeaders.Add(new ListViewColumnHeader(""));
    }

    // MenuControl draws no border of its own - it delegates entirely to its internal ListView -
    // so the inherited focus-swap in Control.GotFocus/LostFocus needs to reach that instead.
    public override Color BorderColour
    {
        get => menuControl.BorderColour;
        set => menuControl.BorderColour = value;
    }

    public List<MenuListViewItem>? MenuItems { get; set; }

    // The internal ListView is never added to any Controls collection, so Screen.FocusInternal
    // never sets its Focused directly - without this, its selected row always renders in the
    // muted unfocused colours (ListView.SelectionColours()) even while the menu genuinely has
    // input focus.
    protected override void OnGotFocus() => menuControl.Focused = true;

    protected override void OnLostFocus() => menuControl.Focused = false;

    protected override void OnDraw() => menuControl.Draw();

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled) =>
        menuControl.KeyPressed(keyInfo, ref handled);

    protected override void OnLoad()
    {
        ArgumentNullException.ThrowIfNull(MenuItems);
        
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;
        
        for (int i = 0; i < MenuItems.Count; i++) {
            menuControl.Items.Add(MenuItems[i]);
        }

        menuControl.BorderForegroundColour = appConfig.Theme.ListViewBorderForeground;
        menuControl.BorderBackgroundColour = appConfig.Theme.ListViewBorderBackground;
        menuControl.BackgroundHighlightColour = appConfig.Theme.BackgroundHighlight;
        menuControl.ForegroundHighlightColour = appConfig.Theme.ForegroundHighlight;
        menuControl.BackgroundHighlightInactiveColour = appConfig.Theme.BackgroundHighlightInactive;
        menuControl.ForegroundHighlightInactiveColour = appConfig.Theme.ForegroundHighlightInactive;
        menuControl.BackgroundColour = appConfig.Theme.ListViewBackground;
        menuControl.ForegroundColour = appConfig.Theme.ListViewForeground;
        menuControl.HeaderBackgroundColour = appConfig.Theme.HeaderBackground;
        menuControl.HeaderForegroundColour = appConfig.Theme.HeaderForeground;
        menuControl.ItemClicked += OnMenuItemClicked;

        base.OnLoad();
    }

    private void OnMenuItemClicked(object? sender, ListViewItemEventArgs e)
    {
        if (e.Item is MenuListViewItem menuItem) {
            MenuItemClicked?.Invoke(sender,  new MenuItemEventArgs(menuItem));
        }
    }
        

    protected override void OnResize()
    {
        menuControl.X = X;
        menuControl.Y = Y;
        menuControl.Width = Width;
        menuControl.Height = Height;
        menuControl.ColumnHeaders[0].Width = Width - 2;

        base.OnResize();
    }

    protected override void OnUnload()
    {
        menuControl.Items.Clear();
        menuControl.ItemClicked -= OnMenuItemClicked;

        base.OnUnload();
    }
}