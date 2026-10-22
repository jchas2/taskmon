using System.Diagnostics;
using System.Drawing;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;

namespace Task.Monitor.Gui.Controls;

public sealed class MenuControl : Control
{
    private readonly ListView  menuControl;
    private readonly AppConfig appConfig;

    public event EventHandler<MenuItemEventArgs>? MenuItemClicked;

    public MenuControl(ISystemTerminal terminal, AppConfig appConfig) : base(terminal)
    {
        this.appConfig = appConfig;
        
        menuControl = new ListView(terminal) {
            ShowColumnHeaders = false,
            ShowCheckboxes    = false,
            Visible           = true
        };
        
        menuControl.ColumnHeaders.Add(new ListViewColumnHeader(""));
    }

    public override Color BorderColour
    {
        get => menuControl.BorderColour;
        set => menuControl.BorderColour = value;
    }

    public override Color DisplayBorderColour => menuControl.DisplayBorderColour;

    public List<MenuListViewItem>? MenuItems { get; set; }

    protected override void OnDraw() => menuControl.Draw();

    protected override void OnGotFocus() => menuControl.Focused = true;

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled) =>
        menuControl.KeyPressed(keyInfo, ref handled);

    protected override void OnLoad()
    {
        ArgumentNullException.ThrowIfNull(MenuItems);
        
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(menuControl, appConfig.Theme);
        
        for (int i = 0; i < MenuItems.Count; i++) {
            menuControl.Items.Add(MenuItems[i]);
        }

        menuControl.ItemClicked += OnMenuItemClicked;

        base.OnLoad();
    }

    protected override void OnLostFocus() => menuControl.Focused = false;

    private void OnMenuItemClicked(object? sender, ListViewItemEventArgs e)
    {
        if (e.Item is MenuListViewItem menuItem) {
            Trace.WriteLine($"MenuControl.MenuItemClicked = {menuItem.Text}");
            MenuItemClicked?.Invoke(sender,  new MenuItemEventArgs(menuItem));
        }
    }
        

    protected override void OnResize()
    {
        menuControl.X      = X;
        menuControl.Y      = Y;
        menuControl.Width  = Width;
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
