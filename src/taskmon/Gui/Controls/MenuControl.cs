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

    public List<MenuListViewItem>? MenuItems { get; set; }

    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();
            menuControl.Draw();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        try {
            Control.DrawingLockAcquire();
            menuControl.KeyPressed(keyInfo, ref handled);
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    protected override void OnLoad()
    {
        ArgumentNullException.ThrowIfNull(MenuItems);
        
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;
        
        for (int i = 0; i < MenuItems.Count; i++) {
            menuControl.Items.Add(MenuItems[i]);
        }

        menuControl.BorderColour = appConfig.DefaultTheme.ListViewBorder;
        menuControl.BackgroundHighlightColour = appConfig.DefaultTheme.BackgroundHighlight;
        menuControl.ForegroundHighlightColour = appConfig.DefaultTheme.ForegroundHighlight;
        menuControl.BackgroundColour = appConfig.DefaultTheme.Background;
        menuControl.ForegroundColour = appConfig.DefaultTheme.Foreground;
        menuControl.HeaderBackgroundColour = appConfig.DefaultTheme.HeaderBackground;
        menuControl.HeaderForegroundColour = appConfig.DefaultTheme.HeaderForeground;
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