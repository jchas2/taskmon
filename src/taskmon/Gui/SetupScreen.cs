using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Controls.MessageBox;
using Task.Monitor.System.Screens;

namespace Task.Monitor.Gui;

public class SetupScreen : Screen
{
    private readonly RunContext runContext;
    private readonly ListView headerView;
    private readonly ListView menuView;
    private readonly ListView generalView;
    private readonly ListView columnsView;
    private readonly ListView themeView;
    private readonly ListView layoutView;
    private readonly ListView metreView;
    private readonly ListView delayView;
    private readonly ListView limitView;
    private readonly ListView numProcsView;
    private readonly List<ListView> tabControls = [];

    private Theme previewTheme;

    private const int ControlGutter = 1;
    private const int MenuViewWidth = 22;
    private const int CommandLength = 10;

    private static readonly (Statistics Statistic, string Label)[] toggleableColumns =
    [
        (Statistics.User, "User"),
        (Statistics.Pri, "Priority"),
        (Statistics.Cpu, "CPU %"),
        (Statistics.AvgCpu, "Average CPU %"),
        (Statistics.MaxCpu, "Max CPU %"),
        (Statistics.Thrd, "Threads"),
        (Statistics.Gpu, "GPU %"),
        (Statistics.AvgGpu, "Average GPU %"),
        (Statistics.MaxGpu, "Max GPU %"),
        (Statistics.Mem, "Memory"),
        (Statistics.AvgMem, "Average Memory"),
        (Statistics.MaxMem, "Max Memory"),
        (Statistics.Disk, "Disk"),
        (Statistics.AvgDisk, "Average Disk"),
        (Statistics.MaxDisk, "Max Disk"),
        (Statistics.Path, "Path"),
    ];

    public SetupScreen(RunContext runContext) : base(runContext.Terminal)
    {
        this.runContext = runContext;

        headerView = new(runContext.Terminal) {
            Name = nameof(headerView),
            TabIndex = 0,
            TabStop = false,
            ShowColumnHeaders = false,
            EnableRowSelect = false,
            EnableScroll = false,
        };

        headerView.ColumnHeaders.Add(new ListViewColumnHeader("SETUP"));
        
        menuView = new(runContext.Terminal) {
            Name = nameof(menuView),
            TabIndex = 1,
            TabStop = true
        };
        
        menuView.ColumnHeaders.Add(new ListViewColumnHeader("CATEGORIES"));

        generalView = new(runContext.Terminal) {
            Name = nameof(generalView),
            EnableScroll = true,
            ShowCheckboxes = true,
            ShowColumnHeaders = true,
            TabIndex = 2,
            TabStop = true,
            Visible = false
        };

        generalView.ColumnHeaders.Add(new ListViewColumnHeader("General Settings"));
        generalView.ColumnHeaders.Add(new ListViewColumnHeader("key"));

        columnsView = new(runContext.Terminal) {
            Name = nameof(columnsView),
            EnableScroll = true,
            ShowCheckboxes = true,
            ShowColumnHeaders = true,
            TabIndex = 3,
            TabStop = true,
            Visible = false
        };

        columnsView.ColumnHeaders.Add(new ListViewColumnHeader("Visible Columns"));
        columnsView.ColumnHeaders.Add(new ListViewColumnHeader("key"));

        themeView = new(runContext.Terminal) {
            Name = nameof(themeView),
            EnableScroll = true,
            ShowColumnHeaders = true,
            TabIndex = 3,
            TabStop = true,
            Visible = false
        };

        themeView.ColumnHeaders.Add(new ListViewColumnHeader("Available themes"));

        layoutView = new(runContext.Terminal) {
            Name = nameof(layoutView),
            EnableScroll = true,
            ShowColumnHeaders = true,
            TabIndex = 3,
            TabStop = true,
            Visible = false
        };

        layoutView.ColumnHeaders.Add(new ListViewColumnHeader("Layouts"));
        
        metreView = new(runContext.Terminal) {
            Name = nameof(metreView),
            EnableScroll = true,
            ShowColumnHeaders = true,
            TabIndex = 4,
            TabStop = true,
            Visible = false
        };

        metreView.ColumnHeaders.Add(new ListViewColumnHeader("Metre Styles"));
        
        delayView = new(runContext.Terminal) {
            Name = nameof(delayView),
            EnableScroll = true,
            ShowColumnHeaders = true,
            TabIndex = 5,
            TabStop = true,
            Visible = false
        };

        delayView.ColumnHeaders.Add(new ListViewColumnHeader("Delay between updates, in milliseconds"));
        
        limitView = new(runContext.Terminal) {
            Name = nameof(limitView),
            EnableScroll = true,
            ShowColumnHeaders = true,
            TabIndex = 6,
            TabStop = true,
            Visible = false
        };

        limitView.ColumnHeaders.Add(new ListViewColumnHeader("Limit the number of process iterations, 0 = loop forever"));
        
        numProcsView = new(runContext.Terminal) {
            Name = nameof(numProcsView),
            EnableScroll = true,
            ShowColumnHeaders = true,
            TabIndex = 7,
            TabStop = true,
            Visible = false
        };

        numProcsView.ColumnHeaders.Add(new ListViewColumnHeader("Number of processes to display, -1 for all"));

        Controls
            .Add(headerView)
            .Add(menuView)
            .Add(generalView)
            .Add(columnsView)
            .Add(themeView)
            .Add(layoutView)
            .Add(metreView)
            .Add(delayView)
            .Add(limitView)
            .Add(numProcsView);
        
        tabControls.AddRange(new [] {
            generalView,
            columnsView,
            themeView,
            layoutView,
            metreView,
            delayView, 
            limitView,
            numProcsView
        });

        previewTheme = runContext.AppConfig.DefaultTheme;
    }

    private void LoadGeneralSection()
    {
        void AddGeneralItem(string text, string key, bool value)
        {
            generalView.Items.Add(new ListViewItem([text, key]));
            generalView.Items[^1].Checked = value;
        }

        AddGeneralItem(
            "Confirm Task delete",
            Constants.Keys.ConfirmTaskDelete,
            runContext.AppConfig.ConfirmTaskDelete);
        
#if __WIN32__
        AddGeneralItem(
            "Highlight Windows Services",
            Constants.Keys.HighlightDaemons,
            runContext.AppConfig.HighlightDaemons);
#endif
#if __APPLE__
        AddGeneralItem(
            "Highlight daemons",
            Constants.Keys.HighlightDaemons,
            runContext.AppConfig.HighlightDaemons);
#endif
        AddGeneralItem(
            "Highlight changed values", 
            Constants.Keys.HighlightStatsColUpdate,
            runContext.AppConfig.HighlightStatisticsColumnUpdate);

        AddGeneralItem(
            "Enable multiple process selection",
            Constants.Keys.MultiSelectProcesses,
            runContext.AppConfig.MultiSelectProcesses);

        AddGeneralItem(
            "Show Cpu meter numerically",
            Constants.Keys.ShowMetreCpuNumerically,
            runContext.AppConfig.ShowMetreCpuNumerically);

        AddGeneralItem(
            "Show Gpu meter numerically",
            Constants.Keys.ShowMetreGpuNumerically,
            runContext.AppConfig.ShowMetreGpuNumerically);
        
        AddGeneralItem(
            "Show Memory metre numerically", 
            Constants.Keys.ShowMetreMemNumerically,
            runContext.AppConfig.ShowMetreMemoryNumerically);

        AddGeneralItem(
            "Show Gpu Memory metre numerically", 
            Constants.Keys.ShowMetreGpuMemNumerically,
            runContext.AppConfig.ShowMetreGpuMemNumerically);
#if __WIN32__
        AddGeneralItem(
            "Show Virtual memory numerically", 
            Constants.Keys.ShowMetreSwapNumerically,
            runContext.AppConfig.ShowMetreSwapNumerically);
#endif
#if __APPLE__
        AddGeneralItem(
            "Show Swap memory numerically", 
            Constants.Keys.ShowMetreSwapNumerically,
            runContext.AppConfig.ShowMetreSwapNumerically);
#endif
        AddGeneralItem(
            "Show Disk metre numerically", 
            Constants.Keys.ShowMetreDiskNumerically,
            runContext.AppConfig.ShowMetreDiskNumerically);

        AddGeneralItem(
            "Show Network metre numerically", 
            Constants.Keys.ShowMetreNetworkNumerically,
            runContext.AppConfig.ShowMetreNetworkNumerically);

        AddGeneralItem(
            "Use large charts",
            Constants.Keys.UseLargeCharts,
            runContext.AppConfig.UseLargeCharts);
        
        AddGeneralItem(
#if __WIN32__
            "Use Irix mode for process CPU% (Unix default)",
#endif
#if __APPLE__
            "Use Irix mode for process CPU% (Activity Monitor)",
#endif
            Constants.Keys.UseIrixCpuReporting,
            runContext.AppConfig.UseIrixReporting);
    }
    
    private void LoadColumnsSection()
    {
        Statistics visibleColumns = runContext.AppConfig.VisibleColumns;

        foreach ((Statistics statistic, string label) in toggleableColumns) {
            columnsView.Items.Add(new ListViewItem([label, statistic.ToString()]));
            columnsView.Items[^1].Checked = (visibleColumns & statistic) != 0;
        }
    }

    private void LoadMenuItems()
    {
        menuView.Items.Add(
            new MenuListViewItem(
                generalView,
                "GENERAL"));

        menuView.Items.Add(
            new MenuListViewItem(
                columnsView,
                "COLUMNS"));

        menuView.Items.Add(
            new MenuListViewItem(
                themeView, 
                "THEMES"));

        menuView.Items.Add(
            new MenuListViewItem(
                layoutView, 
                "LAYOUTS"));
        
        menuView.Items.Add(
            new MenuListViewItem(
                metreView,
                "METRES"));

        menuView.Items.Add(
            new MenuListViewItem(
                delayView,
                "DELAY"));

        menuView.Items.Add(
            new MenuListViewItem(
                limitView,
                "LIMIT"));

        menuView.Items.Add(
            new MenuListViewItem(
                numProcsView,
                "PROCESSES"));
    }

    private void LoadHeaderView()
    {
        headerView.Items.AddRange(
            new ListViewItem("Changes are saved to the following config file:"),
            new ListViewItem(runContext.AppConfig.DefaultConfigFilePath ?? string.Empty));
    }
    
    private void LoadSectionConfigListView<T>(
        ListView listView,
        List<T> values,
        T value)
    {
        int index = values.BinarySearch(value);

        if (index < 0) {
            values.Insert(-index, value);
        }
        
        for (int i = 0; i < values.Count; i++) {
            listView.Items.Add(new ListViewItem(values[i]?.ToString() ?? string.Empty));
            
            if (values[i]!.Equals(value)) {
                listView.SelectedIndex = i;
            }
        }
    }

    private void LoadUxSection()
    {
        void AddItems(ListView listView, List<string> items, Func<string, bool> func)
        {
            for (int i = 0; i < items.Count; i++) {
                listView.Items.Add(new ListViewItem(items[i]));

                if (func(items[i])) {
                    listView.SelectedIndex = i;
                }
            }
        }
        
        List<string> themeNames = runContext.AppConfig.Themes
            .Where(t => t.Name.StartsWith("theme-", StringComparison.CurrentCultureIgnoreCase))
            .Select(t => t.Name)
            .ToList();

        AddItems(themeView, themeNames, val => runContext.AppConfig.DefaultTheme.Name.Equals(val));
        
        List<string> layoutNames = runContext.AppConfig.Layouts
            .Where(t => t.Name.StartsWith("layout-", StringComparison.CurrentCultureIgnoreCase))
            .Select(t => t.Name)
            .ToList();

        AddItems(layoutView, layoutNames, val => runContext.AppConfig.DefaultLayout.Name.Equals(val));
        
        List<string> metreStyles = Enum.GetValues<MetreControlStyle>()
            .Select(c => c.ToString())
            .ToList();

        AddItems(metreView, metreStyles, val => runContext.AppConfig.MetreStyle.ToString().Equals(val));
    }

    private void MapControlsToConfig()
    {
        void UpdateConfigValue(ListViewItem? sourceItem, Action<int> action)
        {
            if (sourceItem?.Text != null) {
                action(int.Parse(sourceItem.Text));
            }
        }

        ListViewItem GetItemValueByKey(string key) => generalView.Items.Single(lvi => lvi.SubItems[1].Text == key);

        runContext.AppConfig.ConfirmTaskDelete = GetItemValueByKey(Constants.Keys.ConfirmTaskDelete).Checked;
        runContext.AppConfig.HighlightDaemons = GetItemValueByKey(Constants.Keys.HighlightDaemons).Checked;
        runContext.AppConfig.HighlightStatisticsColumnUpdate = GetItemValueByKey(Constants.Keys.HighlightStatsColUpdate).Checked;
        runContext.AppConfig.MultiSelectProcesses = GetItemValueByKey(Constants.Keys.MultiSelectProcesses).Checked;
        runContext.AppConfig.ShowMetreCpuNumerically = GetItemValueByKey(Constants.Keys.ShowMetreCpuNumerically).Checked;
        runContext.AppConfig.ShowMetreGpuNumerically = GetItemValueByKey(Constants.Keys.ShowMetreGpuNumerically).Checked;
        runContext.AppConfig.ShowMetreMemoryNumerically = GetItemValueByKey(Constants.Keys.ShowMetreMemNumerically).Checked;
        runContext.AppConfig.ShowMetreGpuMemNumerically = GetItemValueByKey(Constants.Keys.ShowMetreGpuMemNumerically).Checked;
        runContext.AppConfig.ShowMetreSwapNumerically = GetItemValueByKey(Constants.Keys.ShowMetreSwapNumerically).Checked;
        runContext.AppConfig.ShowMetreDiskNumerically = GetItemValueByKey(Constants.Keys.ShowMetreDiskNumerically).Checked;
        runContext.AppConfig.ShowMetreNetworkNumerically = GetItemValueByKey(Constants.Keys.ShowMetreNetworkNumerically).Checked;
        runContext.AppConfig.UseLargeCharts = GetItemValueByKey(Constants.Keys.UseLargeCharts).Checked;
        runContext.AppConfig.UseIrixReporting = GetItemValueByKey(Constants.Keys.UseIrixCpuReporting).Checked;

        Statistics visibleColumns = Statistics.Process | Statistics.Pid;

        foreach (ListViewItem item in columnsView.Items) {
            if (item.Checked && Enum.TryParse(item.SubItems[1].Text, out Statistics statistic)) {
                visibleColumns |= statistic;
            }
        }

        runContext.AppConfig.VisibleColumns = visibleColumns;
        
        if (themeView.SelectedItem?.Text != null) {
            runContext.AppConfig.DefaultTheme = runContext.AppConfig.Themes.First(
                t => t.Name.Equals(themeView.SelectedItem.Text, StringComparison.CurrentCultureIgnoreCase));
        }

        if (layoutView.SelectedItem?.Text != null) {
            runContext.AppConfig.DefaultLayout = runContext.AppConfig.Layouts.First(
                t => t.Name.Equals(layoutView.SelectedItem.Text, StringComparison.CurrentCultureIgnoreCase));
        }

        runContext.AppConfig.MetreStyle = Enum.GetValues<MetreControlStyle>()
            .Single(c => c.ToString() == metreView.SelectedItem?.Text);

        UpdateConfigValue(delayView.SelectedItem,    val => runContext.AppConfig.DelayInMilliseconds = val);
        UpdateConfigValue(limitView.SelectedItem,    val => runContext.AppConfig.IterationLimit = val);
        UpdateConfigValue(numProcsView.SelectedItem, val => runContext.AppConfig.NumberOfProcesses = val);
    }
    
    private void MenuViewOnItemClicked(object? sender, ListViewItemEventArgs e)
    {
        tabControls.ForEach(ctrl => ctrl.Visible = false);
        
        var menuListViewItem = e.Item as MenuListViewItem;
        menuListViewItem!.AssociatedControl.Visible = true;
        
        Clear();
        Resize();
        Draw();
    }

    protected override void OnDraw()
    {
        DrawRectangle(
            X,
            Y,
            Width,
            Height,
            previewTheme.Background);

        Terminal.SetCursorPosition(X, Y);
        Terminal.BackgroundColor = previewTheme.MenubarBackground;
        Terminal.ForegroundColor = previewTheme.MenubarForeground;

        string menubar = "TASK MONITOR SETUP";
        int offsetX = Terminal.WindowWidth / 2 - menubar.Length / 2;
        
        Terminal.WriteEmptyLineTo(offsetX);
        Terminal.Write(menubar.ToBold());
        Terminal.WriteEmptyLineTo(Width - offsetX - menubar.Length);

        Terminal.BackgroundColor = previewTheme.Background;
        Terminal.ForegroundColor = previewTheme.Foreground;
        
        UpdateTheme(headerView);
        headerView.Draw();
        
        UpdateTheme(menuView);
        menuView.Draw();

        foreach (ListView ctrl in tabControls) {
            UpdateTheme(ctrl);
        }
        
        ListView activeControl = tabControls.Single(ctrl => ctrl.Visible);
        activeControl.Draw();

        KeyBindControl.Draw(
            "F10",
            "Done",
            X,
            Height - ControlGutter,
            CommandLength,
            previewTheme,
            enabled: true,
            Terminal);
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        base.OnKeyPressed(keyInfo, ref handled);

        if (handled) {
            return;
        }
        
        ListView activeControl = tabControls.Single(ctrl => ctrl.Visible);
        Control? focusedControl = GetFocusedControl;

        switch (keyInfo.Key) {
            case ConsoleKey.LeftArrow:
                menuView.SetFocus();

                if (menuView.SelectedItem != null) {
                    ListViewItemEventArgs e = new(menuView.SelectedItem);
                    MenuViewOnItemClicked(this, e);
                }

                break;

            case ConsoleKey.RightArrow:
                activeControl.SetFocus();
                Draw();
                break;

            case ConsoleKey.UpArrow:
            case ConsoleKey.DownArrow:
            case ConsoleKey.PageUp:
            case ConsoleKey.PageDown:
            case ConsoleKey.Spacebar:
                focusedControl?.KeyPressed(keyInfo, ref handled);
                break;
            
            case ConsoleKey.F10:
                SaveConfig();
                break;
        }
    }

    protected override void OnLoad()
    {
        Terminal.CursorVisible = false;
        
        foreach (Control control in Controls) {
            control.Load();
        }
        
        foreach (ListView listView in tabControls) {
            listView.Visible = false;
        }

        LoadHeaderView();
        LoadMenuItems();
        LoadGeneralSection();
        LoadColumnsSection();
        LoadUxSection();
        
        LoadSectionConfigListView(
            delayView,
            [ 1000, 1500, 2000, 5000, 10000 ],
            runContext.AppConfig.DelayInMilliseconds);        

        LoadSectionConfigListView(
            numProcsView,
            [ -1, 5, 10, 20, 50, 100, 500, 1000 ],
            runContext.AppConfig.NumberOfProcesses);        
        
        LoadSectionConfigListView(
            limitView,
            [ 0, 1, 3, 5, 10, 20, 50, 100, 500, 1000 ],
            runContext.AppConfig.IterationLimit);

        previewTheme = runContext.AppConfig.DefaultTheme;
        generalView.Visible = true;
        menuView.SetFocus();
        
        menuView.ItemClicked += MenuViewOnItemClicked;
        themeView.ItemClicked += ThemeViewOnItemClicked;
    }

    private void ThemeViewOnItemClicked(object? sender, ListViewItemEventArgs e)
    {
        Theme theme = runContext.AppConfig.Themes
            .Where(t => t.Name.Equals(e.Item.Text, StringComparison.CurrentCultureIgnoreCase))
            .First();

        previewTheme = theme;
        Draw();
    }

    protected override void OnResize()
    {
        base.OnResize();
        Clear();

        headerView.X = X;
        headerView.Y = Y + 2;
        headerView.Width = Width;
        headerView.Height = 3;
        headerView.ColumnHeaders[0].Width = Width;
        
        menuView.X = X;
        menuView.Y = headerView.Y + headerView.Height;
        menuView.Height = Height - (headerView.Height + 4) - ControlGutter;
        menuView.Width = MenuViewWidth;
        menuView.ColumnHeaders[0].Width = MenuViewWidth;
        menuView.Resize();
        
        foreach (ListView ctrl in tabControls) {
            ctrl.X = menuView.X + menuView.Width + ControlGutter;
            ctrl.Y = menuView.Y;
            ctrl.Height = menuView.Height;
            ctrl.Width = Width - (menuView.Width + ControlGutter);
            ctrl.ColumnHeaders[0].Width = ctrl.ShowCheckboxes ? ctrl.Width - ListView.CheckboxWidth : ctrl.Width;
            ctrl.Resize();
        }

        generalView.ColumnHeaders[1].Width = 0;
        columnsView.ColumnHeaders[1].Width = 0;
    }

    protected override void OnUnload()
    {
        base.OnUnload();
        
        menuView.ItemClicked -= MenuViewOnItemClicked;
        themeView.ItemClicked -= ThemeViewOnItemClicked;

        headerView.Items.Clear();
        menuView.Items.Clear();
        
        foreach (Control control in Controls) {
            control.Unload();
            (control as ListView)?.Items.Clear();
        }

        Terminal.CursorVisible = true;
    }

    private void SaveConfig()
    {
        MapControlsToConfig();
                
        if (!runContext.AppConfig.TrySave(runContext.AppConfig.DefaultConfigFilePath ?? string.Empty)) {
            ShowMessageBox(
                "Save Failed",
                "An error occurred saving config.",
                MessageBoxButtons.Ok,
                () => { });
        }
    }

    private void UpdateTheme(ListView listView)
    {
        listView.BackgroundHighlightColour = previewTheme.BackgroundHighlight;
        listView.ForegroundHighlightColour = previewTheme.ForegroundHighlight;
        listView.BackgroundColour = previewTheme.Background;
        listView.ForegroundColour = previewTheme.Foreground;
        listView.HeaderBackgroundColour = previewTheme.HeaderBackground;
        listView.HeaderForegroundColour = previewTheme.HeaderForeground;
    } 
}
