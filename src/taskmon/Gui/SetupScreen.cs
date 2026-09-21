using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Controls.MessageBox;
using Task.Monitor.System.Screens;
using ProcessService = Task.Monitor.System.Services.Process.ProcessService;

namespace Task.Monitor.Gui;

public class SetupScreen : Screen
{
    private readonly RunContext runContext;
    private readonly ScreenApplication screenApp;

    // First row of the LAYOUTS tab - Enter on it opens the designer on a fresh example layout.
    // Can't collide with a real layout name: the designer's save dialog doesn't accept '+'.
    private const string NewLayoutRow = "+ New Layout";

    // Set when leaving for the designer, so the Show() that brings this screen back (after the
    // designer is closed) returns to the LAYOUTS tab rather than resetting to GENERAL.
    private bool returnToLayoutsTab;
    private readonly ListView headerView;
    private readonly ListView menuView;
    private readonly ListView generalView;
    private readonly ListView columnsView;
    private readonly ListView themeView;
    private readonly ListView layoutView;
    private readonly ListView metreView;
    private readonly ListView delayView;
    private readonly ListView numProcsView;
    private readonly List<ListView> tabControls = [];

    private Theme previewTheme;
    private bool preferIndexedColours;

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
        (Statistics.Power, "Power"),
        (Statistics.Path, "Path"),
    ];

    public SetupScreen(RunContext runContext, ScreenApplication screenApp) : base(runContext.Terminal)
    {
        this.runContext = runContext;
        this.screenApp = screenApp;

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

        layoutView.ColumnHeaders.Add(new ListViewColumnHeader("Summary layouts - ↵ create/edit, highlighted is used on F10 Done"));
        
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
        
        numProcsView = new(runContext.Terminal) {
            Name = nameof(numProcsView),
            EnableScroll = true,
            ShowColumnHeaders = true,
            TabIndex = 6,
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
            .Add(numProcsView);
        
        tabControls.AddRange(new [] {
            generalView,
            columnsView,
            themeView,
            layoutView,
            metreView,
            delayView, 
            numProcsView
        });

        previewTheme = runContext.AppConfig.Theme;
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
            "Show Cpu chart label numerically",
            Constants.Keys.ShowMetreCpuNumerically,
            runContext.AppConfig.ShowMetreCpuNumerically);

        AddGeneralItem(
            "Show Gpu chart label numerically",
            Constants.Keys.ShowMetreGpuNumerically,
            runContext.AppConfig.ShowMetreGpuNumerically);
        
        AddGeneralItem(
            "Show Memory chart label numerically", 
            Constants.Keys.ShowMetreMemNumerically,
            runContext.AppConfig.ShowMetreMemoryNumerically);

        AddGeneralItem(
            "Show Gpu Memory chart label numerically", 
            Constants.Keys.ShowMetreGpuMemNumerically,
            runContext.AppConfig.ShowMetreGpuMemNumerically);
#if __WIN32__
        AddGeneralItem(
            "Show Virtual memory chart label numerically", 
            Constants.Keys.ShowMetreSwapNumerically,
            runContext.AppConfig.ShowMetreSwapNumerically);
#endif
#if __APPLE__
        AddGeneralItem(
            "Show Swap memory chart label numerically", 
            Constants.Keys.ShowMetreSwapNumerically,
            runContext.AppConfig.ShowMetreSwapNumerically);
#endif
        AddGeneralItem(
            "Show Disk chart label numerically", 
            Constants.Keys.ShowMetreDiskNumerically,
            runContext.AppConfig.ShowMetreDiskNumerically);

        AddGeneralItem(
            "Show Network chart label numerically", 
            Constants.Keys.ShowMetreNetworkNumerically,
            runContext.AppConfig.ShowMetreNetworkNumerically);

        AddGeneralItem(
            "Show chart Y axis scale", 
            Constants.Keys.ShowYAxisScale,
            runContext.AppConfig.ShowYAxisScale);

        AddGeneralItem(
#if __WIN32__
            "Use Irix mode for per-process CPU% (individual core saturation)",
#endif
#if __APPLE__
            "Use Irix mode for per-process CPU% (Activity Monitor)",
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
            .OrderBy(t => t.Name)
            .Select(t => t.Name)
            .ToList();

        AddItems(themeView, themeNames, val => runContext.AppConfig.Theme.Name.Equals(val));
        
        // The SUMMARY screen's split-tree layouts (SummaryControl2) - the shipped ones and any
        // saved from the designer - with the one currently in use highlighted.
        layoutView.Items.Add(new ListViewItem(NewLayoutRow));

        string? defaultLayoutName = runContext.AppConfig.DefaultSummaryLayout2?.Name;

        foreach (string name in runContext.AppConfig.SummaryLayouts2.Select(l => l.Name).OrderBy(n => n)) {
            layoutView.Items.Add(new ListViewItem(name));

            if (name.Equals(defaultLayoutName, StringComparison.CurrentCultureIgnoreCase)) {
                layoutView.SelectedIndex = layoutView.Items.Count - 1;
            }
        }
        
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
        runContext.AppConfig.ShowYAxisScale = GetItemValueByKey(Constants.Keys.ShowYAxisScale).Checked;
        runContext.AppConfig.UseIrixReporting = GetItemValueByKey(Constants.Keys.UseIrixCpuReporting).Checked;

        Statistics visibleColumns = Statistics.Process | Statistics.Pid;

        foreach (ListViewItem item in columnsView.Items) {
            if (item.Checked && Enum.TryParse(item.SubItems[1].Text, out Statistics statistic)) {
                visibleColumns |= statistic;
            }
        }

        runContext.AppConfig.VisibleColumns = visibleColumns;
        
        if (themeView.SelectedItem?.Text != null) {
            runContext.AppConfig.Theme = runContext.AppConfig.Themes.First(
                t => t.Name.Equals(themeView.SelectedItem.Text, StringComparison.CurrentCultureIgnoreCase));
        }

        // The "+ New Layout" row matches no layout, so leaving it highlighted keeps the current one.
        SummaryLayout2? chosenLayout = runContext.AppConfig.SummaryLayouts2.FirstOrDefault(
            l => l.Name.Equals(layoutView.SelectedItem?.Text, StringComparison.CurrentCultureIgnoreCase));

        if (chosenLayout != null) {
            runContext.AppConfig.DefaultSummaryLayout2 = chosenLayout;
        }

        runContext.AppConfig.MetreStyle = Enum.GetValues<MetreControlStyle>()
            .Single(c => c.ToString() == metreView.SelectedItem?.Text);

        UpdateConfigValue(delayView.SelectedItem,    val => runContext.AppConfig.DelayInMilliseconds = val);
        UpdateConfigValue(numProcsView.SelectedItem, val => runContext.AppConfig.NumberOfProcesses = val);
        
        ApplySamplingSettings();
    }

    // The two settings above that are inputs to the sampling rather than to how it is drawn. They
    // are pushed onto the services themselves: nothing republishes AppConfig, so a service already
    // running would otherwise keep sampling with whatever it was given at startup.
    //
    // The delay goes to every service and to the controller's publish cycle, so changing it here
    // takes effect on the charts and the repaint rate as well as on the process list. Each service
    // picks it up within one 250ms step of its current wait rather than at the end of it.
    private void ApplySamplingSettings()
    {
        runContext.ServiceController.SetSamplingDelay(runContext.AppConfig.DelayInMilliseconds);
        runContext.ServiceController.GetService<ProcessService>().IrixMode =
            runContext.AppConfig.UseIrixReporting;
    }
    
    private void MenuViewOnItemClicked(object? sender, ListViewItemEventArgs e)
    {
        tabControls.ForEach(ctrl => ctrl.Visible = false);
        
        var menuListViewItem = e.Item as MenuListViewItem;
        menuListViewItem!.AssociatedControl.Visible = true;
        
        Draw();
    }

    protected override void OnDraw()
    {
        Terminal.SetCursorPosition(X, Y);
        Terminal.BackgroundColor = previewTheme.MenubarBackground;
        Terminal.ForegroundColor = previewTheme.MenubarForeground;

        string menubar = "TASK MONITOR SETUP";
        int offsetX = Terminal.WindowWidth / 2 - menubar.Length / 2;
        
        Terminal.WriteEmptyLineTo(offsetX);
        Terminal.Write(menubar);
        Terminal.WriteEmptyLineTo(Width - offsetX - menubar.Length);

        UpdateTheme();
        headerView.Draw();
        menuView.Draw();
        
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
            case ConsoleKey.Escape:
                ConsolePalette.PreferIndexedColours = preferIndexedColours;
                break;
            case ConsoleKey.LeftArrow:
                menuView.SetFocus();

                if (menuView.SelectedItem != null) {
                    ListViewItemEventArgs e = new(menuView.SelectedItem);
                    MenuViewOnItemClicked(this, e);
                }

                handled = true;
                break;

            case ConsoleKey.RightArrow:
                activeControl.SetFocus();
                Draw();
                handled = true;
                break;

            case ConsoleKey.UpArrow:
            case ConsoleKey.DownArrow:
            case ConsoleKey.PageUp:
            case ConsoleKey.PageDown:
            case ConsoleKey.Spacebar:
                focusedControl?.KeyPressed(keyInfo, ref handled);
                handled = true;
                break;

            // Enter on a layout opens it in the designer; on "+ New Layout", a fresh example.
            // Needs the list itself focused (Right arrow into it), like Space/arrows above.
            case ConsoleKey.Enter when activeControl == layoutView && focusedControl == layoutView:
                OpenLayoutDesigner(layoutView.SelectedItem?.Text);
                handled = true;
                break;

            // Shortcut for "+ New Layout" from anywhere on the LAYOUTS tab.
            case ConsoleKey.N when activeControl == layoutView:
                OpenLayoutDesigner(null);
                handled = true;
                break;

            case ConsoleKey.F10:
                if (!SaveConfig()) {
                    handled = true;
                    ShowMessageBox(
                        "Save Failed",
                        "An error occurred saving config.",
                        MessageBoxButtons.Ok,
                        () => { });
                } 
                break;
        }
    }

    protected override void OnLoad()
    {
        Terminal.CursorVisible = false;

        BackgroundColour = runContext.AppConfig.Theme.Background;
        ForegroundColour = runContext.AppConfig.Theme.Foreground;
        
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

        previewTheme = runContext.AppConfig.Theme;
        preferIndexedColours = ConsolePalette.PreferIndexedColours;

        if (returnToLayoutsTab) {
            // Back from the designer - the list above was just rebuilt, so a layout saved there
            // is already in it. Focus itself is set in OnShown: Screen.Show() runs Focus() after
            // Load(), which would move it back to the category menu.
            layoutView.Visible = true;
            SelectMenuItemFor(layoutView);
        }
        else {
            generalView.Visible = true;
            menuView.SetFocus();
        }
        
        menuView.ItemClicked += MenuViewOnItemClicked;
        themeView.ItemClicked += ThemeViewOnItemClicked;
        
        base.OnLoad();
    }
    
    protected override void OnShown()
    {
        if (!returnToLayoutsTab) {
            return;
        }

        returnToLayoutsTab = false;
        layoutView.SetFocus();
    }

    private void SelectMenuItemFor(ListView tab)
    {
        for (int i = 0; i < menuView.Items.Count; i++) {
            if (menuView.Items[i] is MenuListViewItem item && item.AssociatedControl == tab) {
                menuView.SelectedIndex = i;
                return;
            }
        }
    }

    // A null or unknown name (e.g. the "+ New Layout" row) opens a fresh, unnamed layout of one
    // empty pane.
    // The designer edits a copy (ToTree builds a new tree), so leaving it without saving changes
    // nothing here.
    private void OpenLayoutDesigner(string? layoutName)
    {
        SummaryLayout2? layout = runContext.AppConfig.SummaryLayouts2.FirstOrDefault(
            l => l.Name.Equals(layoutName, StringComparison.CurrentCultureIgnoreCase));

        LayoutDesignerScreen designer = screenApp.GetScreen<LayoutDesignerScreen>();
        designer.Open(layout?.ToTree() ?? SummaryLayoutTree.CreateEmpty(), layout?.Name);

        returnToLayoutsTab = true;
        screenApp.ShowScreen<LayoutDesignerScreen>();
    }

    private void ThemeViewOnItemClicked(object? sender, ListViewItemEventArgs e)
    {
        Theme theme = runContext.AppConfig.Themes
            .Where(t => t.Name.Equals(e.Item.Text, StringComparison.CurrentCultureIgnoreCase))
            .First();
        
        previewTheme = theme;
        
        ConsolePalette.PreferIndexedColours = 
            TerminalCapabilities.ResolvePreferIndexed(previewTheme.ColourMode, Environment.GetEnvironmentVariable);

        UpdateTheme();
        Clear();
        Draw();
    }

    // Every ListView here defaults to ShowBorder = true, which insets its actual drawable
    // viewport by one column on each side (see ListView.CalculateViewPortBounds) - a column
    // width set to the control's raw Width, with no allowance for that inset, trips
    // ListView.DrawItem's "does this column fit the viewport" guard on column 0 of every row,
    // silently blanking every list on this screen. This screen was unreachable from the running
    // app until this session wired up MainScreen2's F2 (see MainScreen2's ScreenApplication
    // field), so nothing had ever actually exercised OnResize() against a real terminal before.
    private const int BorderInset = 2;

    protected override void OnResize()
    {
        headerView.X = X;
        headerView.Y = Y + 2;
        headerView.Width = Width;
        headerView.Height = 3;
        headerView.ColumnHeaders[0].Width = Math.Max(0, Width - BorderInset);

        menuView.X = X;
        menuView.Y = headerView.Y + headerView.Height;
        menuView.Height = Height - (headerView.Height + 4) - ControlGutter;
        menuView.Width = MenuViewWidth;
        menuView.ColumnHeaders[0].Width = Math.Max(0, MenuViewWidth - BorderInset);

        foreach (ListView ctrl in tabControls) {
            ctrl.X = menuView.X + menuView.Width + ControlGutter;
            ctrl.Y = menuView.Y;
            ctrl.Height = menuView.Height;
            ctrl.Width = Width - (menuView.Width + ControlGutter);

            int checkboxWidth = ctrl.ShowCheckboxes ? ListView.CheckboxWidth : 0;
            ctrl.ColumnHeaders[0].Width = Math.Max(0, ctrl.Width - checkboxWidth - BorderInset);
        }

        generalView.ColumnHeaders[1].Width = 0;
        columnsView.ColumnHeaders[1].Width = 0;

        base.OnResize();
    }

    protected override void OnUnload()
    {
        menuView.ItemClicked -= MenuViewOnItemClicked;
        themeView.ItemClicked -= ThemeViewOnItemClicked;

        headerView.Items.Clear();
        menuView.Items.Clear();
        
        foreach (Control control in Controls) {
            control.Unload();
            (control as ListView)?.Items.Clear();
        }

        Terminal.CursorVisible = true;
        
        base.OnUnload();
    }

    private bool SaveConfig()
    {
        MapControlsToConfig();
        bool result = runContext.AppConfig.TrySave(runContext.AppConfig.DefaultConfigFilePath ?? string.Empty);

        if (result) {
            Trace.WriteLine($"Config saved = \n{runContext.AppConfig}");
        }

        return result;
    }

    private void UpdateTheme()
    {
        BackgroundColour = previewTheme.Background;
        ForegroundColour = previewTheme.Foreground;

        foreach (Control ctrl in Controls) {
            ctrl.BackgroundColour = previewTheme.Background;
            ctrl.ForegroundColour = previewTheme.Foreground;
        }

        UpdateTheme(menuView);
        
        foreach (ListView ctrl in tabControls) {
            UpdateTheme(ctrl);
        }
    }

    private void UpdateTheme(ListView ctrl)
    {
        ctrl.BackgroundHighlightColour = previewTheme.BackgroundHighlight;
        ctrl.ForegroundHighlightColour = previewTheme.ForegroundHighlight;
        ctrl.BackgroundHighlightInactiveColour = previewTheme.BackgroundHighlightInactive;
        ctrl.ForegroundHighlightInactiveColour = previewTheme.ForegroundHighlightInactive;
        ctrl.HeaderBackgroundColour = previewTheme.HeaderBackground;
        ctrl.HeaderForegroundColour = previewTheme.HeaderForeground;
    }
}
