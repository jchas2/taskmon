using System.Diagnostics;
using System.Globalization;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.Gui.Controls.Summary2;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.Resources;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Controls.MessageBox;
using Task.Monitor.System.Screens;
using MessageBoxControl = Task.Monitor.System.Controls.MessageBox.MessageBox;
using ProcessService = Task.Monitor.System.Services.Process.ProcessService;

namespace Task.Monitor.Gui;

public class SetupScreen : Screen
{
    private const int ControlGutter         = 1;
    private const int MenuViewWidth         = 22;
    private const int CommandLength         = 10;
    private const int PreviewMinWidth       = 40;
    private const int PreviewMinHeight      = 14;
    private const int SaveErrorWidth        = 72;

    private readonly RunContext        runContext;
    private readonly ScreenApplication screenApp;
    private readonly ListView          headerView;
    private readonly ListView          menuView;
    private readonly ListView          generalView;
    private readonly ListView          columnsView;
    private readonly ListView          themeView;
    private readonly ListView          layoutView;
    private readonly ListView          metreView;
    private readonly ListView          delayView;
    private readonly ListView          languageView;
    private readonly MessageBoxControl messageBox;
    private readonly List<ListView>    tabControls = [];
    
    private readonly Dictionary<ListView, Func<LayoutTree<SummaryPane>>> previewLayouts;
    private readonly Dictionary<ListView, SummaryControl2>                previews = new();

    private Theme      previewTheme;
    private AppConfig? previewConfig;
    private ListView?  previewTab;
    private string     languageNote = string.Empty;

    private bool preferIndexedColours;
    private bool returnToLayoutsTab;
    private bool previewFits;
    
    // Each label is the column's Statistics_<member> entry in Strings.resx.
    private static readonly Statistics[] ToggleableColumns =
    [
        Statistics.User,
        Statistics.Pri,
        Statistics.Cpu,
        Statistics.AvgCpu,
        Statistics.MaxCpu,
        Statistics.Thrd,
        Statistics.Gpu,
        Statistics.AvgGpu,
        Statistics.MaxGpu,
        Statistics.Mem,
        Statistics.AvgMem,
        Statistics.MaxMem,
        Statistics.Disk,
        Statistics.AvgDisk,
        Statistics.MaxDisk,
        Statistics.Power,
        Statistics.Path,
    ];

    private const int BorderInset = 2;

    public SetupScreen(RunContext runContext, ScreenApplication screenApp) : base(runContext.Terminal)
    {
        this.runContext = runContext;
        this.screenApp  = screenApp;

        messageBox = new MessageBoxControl(runContext.Terminal) {
            Visible = false,
            Buttons = MessageBoxButtons.Ok,
            Height  = MessageBoxControl.PreferredHeight
        };

        CommonControl.Text(messageBox);

        headerView = new(runContext.Terminal) {
            Name  = nameof(headerView),
            TabIndex          = 0,
            TabStop           = false,
            ShowColumnHeaders = false,
            EnableRowSelect   = false,
            EnableScroll      = false,
        };

        headerView.ColumnHeaders.Add(new ListViewColumnHeader("SETUP"));
        
        menuView = new(runContext.Terminal) {
            Name     = nameof(menuView),
            TabIndex = 1,
            TabStop  = true
        };
        
        menuView.ColumnHeaders.Add(new ListViewColumnHeader(Strings.SetupCategoriesHeader));

        generalView = new(runContext.Terminal) {
            Name              = nameof(generalView),
            EnableScroll      = true,
            ShowCheckboxes    = true,
            ShowColumnHeaders = true,
            TabIndex          = 2,
            TabStop           = true,
            Visible           = false
        };

        generalView.ColumnHeaders.Add(new ListViewColumnHeader(Strings.SetupGeneralHeader));
        generalView.ColumnHeaders.Add(new ListViewColumnHeader("key"));

        columnsView = new(runContext.Terminal) {
            Name              = nameof(columnsView),
            EnableScroll      = true,
            ShowCheckboxes    = true,
            ShowColumnHeaders = true,
            TabIndex          = 3,
            TabStop           = true,
            Visible           = false
        };

        columnsView.ColumnHeaders.Add(new ListViewColumnHeader(Strings.SetupColumnsHeader));
        columnsView.ColumnHeaders.Add(new ListViewColumnHeader("key"));

        themeView = new(runContext.Terminal) {
            Name              = nameof(themeView),
            EnableScroll      = true,
            ShowColumnHeaders = true,
            TabIndex          = 3,
            TabStop           = true,
            Visible           = false
        };

        themeView.ColumnHeaders.Add(new ListViewColumnHeader(Strings.SetupThemesHeader));

        layoutView = new(runContext.Terminal) {
            Name              = nameof(layoutView),
            EnableScroll      = true,
            ShowColumnHeaders = true,
            TabIndex          = 3,
            TabStop           = true,
            Visible           = false
        };

        layoutView.ColumnHeaders.Add(new ListViewColumnHeader(Strings.SetupLayoutsHeader));
        
        metreView = new(runContext.Terminal) {
            Name              = nameof(metreView),
            EnableScroll      = true,
            ShowColumnHeaders = true,
            TabIndex          = 4,
            TabStop           = true,
            Visible           = false
        };

        metreView.ColumnHeaders.Add(new ListViewColumnHeader(Strings.SetupMetreStylesHeader));
        metreView.ColumnHeaders.Add(new ListViewColumnHeader("key"));
        
        delayView = new(runContext.Terminal) {
            Name              = nameof(delayView),
            EnableScroll      = true,
            ShowColumnHeaders = true,
            TabIndex          = 5,
            TabStop           = true,
            Visible           = false
        };

        delayView.ColumnHeaders.Add(new ListViewColumnHeader(Strings.SetupDelayHeader));
        delayView.ColumnHeaders.Add(new ListViewColumnHeader("ms"));

        languageView = new(runContext.Terminal) {
            Name              = nameof(languageView),
            EnableScroll      = true,
            ShowColumnHeaders = true,
            TabIndex          = 6,
            TabStop           = true,
            Visible           = false
        };

        languageView.ColumnHeaders.Add(new ListViewColumnHeader(Strings.SetupLanguageHeader));
        languageView.ColumnHeaders.Add(new ListViewColumnHeader("key"));

        Controls
            .Add(headerView)
            .Add(menuView)
            .Add(generalView)
            .Add(columnsView)
            .Add(themeView)
            .Add(layoutView)
            .Add(metreView)
            .Add(delayView)
            .Add(languageView);

        tabControls.AddRange(new [] {
            generalView,
            columnsView,
            themeView,
            layoutView,
            metreView,
            delayView,
            languageView
        });

        previewTheme = runContext.AppConfig.Theme;

        previewLayouts = new() {
            [themeView] = ThemePreviewLayout,
            [metreView] = MetrePreviewLayout
        };
    }

    private string ConfigFileDisplayPath =>
        runContext.AppConfig.DefaultConfigFilePath is { Length: > 0 } path
            ? Path.GetFullPath(path)
            : Strings.CommonNotAvailable;

    private void LoadColumnsSection()
    {
        Statistics visibleColumns = runContext.AppConfig.VisibleColumns;

        foreach (Statistics statistic in ToggleableColumns) {
            columnsView.Items.Add(new ListViewItem([statistic.ToDisplayText(), statistic.ToString()]));
            columnsView.Items[^1].Checked = (visibleColumns & statistic) != 0;
        }
    }

    private void LoadDelaySection()
    {
        int configured = runContext.AppConfig.DelayInMilliseconds;

        List<(string Text, int Milliseconds)> rows = Enum.GetValues<UpdateInterval>()
            .Select(interval => (interval.ToDisplayText(), (int)interval))
            .ToList();

        if (UpdateIntervalExtensions.FromMilliseconds(configured) is null) {
            rows.Add((UpdateIntervalExtensions.CustomDisplayText(configured), configured));
            rows.Sort((a, b) => a.Milliseconds.CompareTo(b.Milliseconds));
        }

        for (int i = 0; i < rows.Count; i++) {
            delayView.Items.Add(new ListViewItem([rows[i].Text, rows[i].Milliseconds.ToString()]));

            if (rows[i].Milliseconds == configured) {
                delayView.SelectedIndex = i;
            }
        }
    }

    private void LoadGeneralSection()
    {
        void AddGeneralItem(string text, string key, bool value)
        {
            generalView.Items.Add(new ListViewItem([text, key]));
            generalView.Items[^1].Checked = value;
        }

        AddGeneralItem(
            Strings.SetupConfirmTaskDelete,
            Constants.Keys.ConfirmTaskDelete,
            runContext.AppConfig.ConfirmTaskDelete);
        
#if __WIN32__
        AddGeneralItem(
            Strings.SetupHighlightWindowsServices,
            Constants.Keys.HighlightDaemons,
            runContext.AppConfig.HighlightDaemons);
#endif
#if __APPLE__
        AddGeneralItem(
            Strings.SetupHighlightDaemons,
            Constants.Keys.HighlightDaemons,
            runContext.AppConfig.HighlightDaemons);
#endif
        AddGeneralItem(
            Strings.SetupHighlightChangedValues,
            Constants.Keys.HighlightStatsColUpdate,
            runContext.AppConfig.HighlightStatisticsColumnUpdate);

        AddGeneralItem(
            Strings.SetupMultiSelectProcesses,
            Constants.Keys.MultiSelectProcesses,
            runContext.AppConfig.MultiSelectProcesses);

        AddGeneralItem(
            Strings.SetupShowCpuNumerically,
            Constants.Keys.ShowMetreCpuNumerically,
            runContext.AppConfig.ShowMetreCpuNumerically);

        AddGeneralItem(
            Strings.SetupShowGpuNumerically,
            Constants.Keys.ShowMetreGpuNumerically,
            runContext.AppConfig.ShowMetreGpuNumerically);
        
#if __APPLE__
        AddGeneralItem(
            Strings.SetupShowNpuNumerically,
            Constants.Keys.ShowMetreNpuNumerically,
            runContext.AppConfig.ShowMetreNpuNumerically);
#endif

        AddGeneralItem(
            Strings.SetupShowMemoryNumerically,
            Constants.Keys.ShowMetreMemNumerically,
            runContext.AppConfig.ShowMetreMemoryNumerically);

        AddGeneralItem(
            Strings.SetupShowGpuMemoryNumerically,
            Constants.Keys.ShowMetreGpuMemNumerically,
            runContext.AppConfig.ShowMetreGpuMemNumerically);
#if __WIN32__
        AddGeneralItem(
            Strings.SetupShowVirtualMemoryNumerically,
            Constants.Keys.ShowMetreSwapNumerically,
            runContext.AppConfig.ShowMetreSwapNumerically);
#endif
#if __APPLE__
        AddGeneralItem(
            Strings.SetupShowSwapNumerically,
            Constants.Keys.ShowMetreSwapNumerically,
            runContext.AppConfig.ShowMetreSwapNumerically);
#endif
        AddGeneralItem(
            Strings.SetupShowDiskNumerically,
            Constants.Keys.ShowMetreDiskNumerically,
            runContext.AppConfig.ShowMetreDiskNumerically);

        AddGeneralItem(
            Strings.SetupShowNetworkNumerically,
            Constants.Keys.ShowMetreNetworkNumerically,
            runContext.AppConfig.ShowMetreNetworkNumerically);

        AddGeneralItem(
            Strings.SetupShowYAxisScale,
            Constants.Keys.ShowYAxisScale,
            runContext.AppConfig.ShowYAxisScale);

        AddGeneralItem(
            Strings.SetupShowLargeChartGrid,
            Constants.Keys.ShowLargeMetreGrid,
            runContext.AppConfig.ShowLargeMetreGrid);

        AddGeneralItem(
            Strings.SetupShowSmallChartGrid,
            Constants.Keys.ShowSmallMetreGrid,
            runContext.AppConfig.ShowSmallMetreGrid);

        AddGeneralItem(
            Strings.SetupShowTips,
            Constants.Keys.ShowTips,
            runContext.AppConfig.ShowTips);

        AddGeneralItem(
#if __WIN32__
            Strings.SetupIrixModeWindows,
#endif
#if __APPLE__
            Strings.SetupIrixModeMacOS,
#endif
            Constants.Keys.UseIrixCpuReporting,
            runContext.AppConfig.UseIrixReporting);
    }

    private void LoadHeaderView()
    {
        headerView.Items.Add(new ListViewItem(string.Format(CultureInfo.CurrentCulture, Strings.SetupConfigFile, ConfigFileDisplayPath)));
    }

    private void LoadLanguageSection()
    {
        string            configured = runContext.AppConfig.UiLanguage;
        UiLanguageChoice? chosen     = UiLanguage.Find(configured);

        languageView.Items.Add(new ListViewItem([
            string.Format(CultureInfo.CurrentCulture, Strings.SetupLanguageSystem, UiLanguage.SystemChoice().NativeName),
            string.Empty]));

        languageView.SelectedIndex = 0;

        foreach (UiLanguageChoice choice in UiLanguage.Choices) {
            languageView.Items.Add(new ListViewItem([choice.NativeName, choice == chosen ? configured : choice.Code]));

            if (choice == chosen) {
                languageView.SelectedIndex = languageView.Items.Count - 1;
            }
        }

        if (chosen == null && !string.IsNullOrWhiteSpace(configured)) {
            languageView.Items.Add(new ListViewItem([
                string.Format(CultureInfo.CurrentCulture, Strings.SetupLanguageUnlisted, configured.Trim()),
                configured]));

            languageView.SelectedIndex = languageView.Items.Count - 1;
        }

        languageNote = UiLanguage.IsOverridden(Environment.GetEnvironmentVariable)
            ? string.Format(CultureInfo.CurrentCulture, Strings.SetupLanguageOverridden, UiLanguage.UiLanguageEnvVar)
            : Strings.SetupLanguageRestartHint;
    }

    private void LoadMenuItems()
    {
        menuView.Items.Add(
            new MenuListViewItem(
                generalView,
                Strings.SetupGeneralMenuItem));

        menuView.Items.Add(
            new MenuListViewItem(
                columnsView,
                Strings.SetupColumnsMenuItem));

        menuView.Items.Add(
            new MenuListViewItem(
                themeView, 
                Strings.SetupThemesMenuItem));

        menuView.Items.Add(
            new MenuListViewItem(
                layoutView, 
                Strings.SetupLayoutsMenuItem));
        
        menuView.Items.Add(
            new MenuListViewItem(
                metreView,
                Strings.SetupMetresMenuItem));

        menuView.Items.Add(
            new MenuListViewItem(
                delayView,
                Strings.SetupDelayMenuItem));

        menuView.Items.Add(
            new MenuListViewItem(
                languageView,
                Strings.SetupLanguageMenuItem));
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
        
        layoutView.Items.Add(new ListViewItem(Strings.SetupNewLayoutRow));

        string? defaultLayoutName = runContext.AppConfig.DefaultLayout?.Name;

        foreach (string name in runContext.AppConfig.Layouts.Select(l => l.Name).OrderBy(n => n)) {
            layoutView.Items.Add(new ListViewItem(name));

            if (name.Equals(defaultLayoutName, StringComparison.CurrentCultureIgnoreCase)) {
                layoutView.SelectedIndex = layoutView.Items.Count - 1;
            }
        }
        
        foreach (MetreControlStyle style in Enum.GetValues<MetreControlStyle>()) {
            metreView.Items.Add(new ListViewItem([style.ToDisplayText(), style.ToString()]));

            if (style == runContext.AppConfig.MetreStyle) {
                metreView.SelectedIndex = metreView.Items.Count - 1;
            }
        }
    }

    private void MapControlsToConfig()
    {
        ListViewItem GetItemValueByKey(string key) => generalView.Items.Single(lvi => lvi.SubItems[1].Text == key);

        runContext.AppConfig.ConfirmTaskDelete               = GetItemValueByKey(Constants.Keys.ConfirmTaskDelete).Checked;
        runContext.AppConfig.HighlightDaemons                = GetItemValueByKey(Constants.Keys.HighlightDaemons).Checked;
        runContext.AppConfig.HighlightStatisticsColumnUpdate = GetItemValueByKey(Constants.Keys.HighlightStatsColUpdate).Checked;
        runContext.AppConfig.MultiSelectProcesses            = GetItemValueByKey(Constants.Keys.MultiSelectProcesses).Checked;
        runContext.AppConfig.ShowMetreCpuNumerically         = GetItemValueByKey(Constants.Keys.ShowMetreCpuNumerically).Checked;
        runContext.AppConfig.ShowMetreGpuNumerically         = GetItemValueByKey(Constants.Keys.ShowMetreGpuNumerically).Checked;
        runContext.AppConfig.ShowMetreMemoryNumerically      = GetItemValueByKey(Constants.Keys.ShowMetreMemNumerically).Checked;
        runContext.AppConfig.ShowMetreGpuMemNumerically      = GetItemValueByKey(Constants.Keys.ShowMetreGpuMemNumerically).Checked;
        runContext.AppConfig.ShowMetreSwapNumerically        = GetItemValueByKey(Constants.Keys.ShowMetreSwapNumerically).Checked;
        runContext.AppConfig.ShowMetreDiskNumerically        = GetItemValueByKey(Constants.Keys.ShowMetreDiskNumerically).Checked;
        runContext.AppConfig.ShowMetreNetworkNumerically     = GetItemValueByKey(Constants.Keys.ShowMetreNetworkNumerically).Checked;
#if __APPLE__
        runContext.AppConfig.ShowMetreNpuNumerically         = GetItemValueByKey(Constants.Keys.ShowMetreNpuNumerically).Checked;
#endif
        runContext.AppConfig.ShowYAxisScale                  = GetItemValueByKey(Constants.Keys.ShowYAxisScale).Checked;
        runContext.AppConfig.ShowLargeMetreGrid              = GetItemValueByKey(Constants.Keys.ShowLargeMetreGrid).Checked;
        runContext.AppConfig.ShowSmallMetreGrid              = GetItemValueByKey(Constants.Keys.ShowSmallMetreGrid).Checked;
        runContext.AppConfig.ShowTips                        = GetItemValueByKey(Constants.Keys.ShowTips).Checked;
        runContext.AppConfig.UseIrixReporting                = GetItemValueByKey(Constants.Keys.UseIrixCpuReporting).Checked;

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

        SummaryControlLayout? chosenLayout = runContext.AppConfig.Layouts.FirstOrDefault(
            l => l.Name.Equals(layoutView.SelectedItem?.Text, StringComparison.CurrentCultureIgnoreCase));

        if (chosenLayout != null) {
            runContext.AppConfig.DefaultLayout = chosenLayout;
        }

        runContext.AppConfig.MetreStyle = Enum.GetValues<MetreControlStyle>()
            .Single(c => c.ToString() == metreView.SelectedItem?.SubItems[1].Text);

        if (delayView.SelectedItem?.SubItems[1].Text is { } delay) {
            runContext.AppConfig.DelayInMilliseconds = int.Parse(delay);
        }

        if (languageView.SelectedItem?.SubItems[1].Text is { } language) {
            runContext.AppConfig.UiLanguage = language;
        }

        runContext.ServiceController.SetSamplingDelay(runContext.AppConfig.DelayInMilliseconds);
        runContext.ServiceController.GetService<ProcessService>().IrixMode = runContext.AppConfig.UseIrixReporting;
    }

    private void MenuViewOnItemClicked(object? sender, ListViewItemEventArgs e)
    {
        tabControls.ForEach(ctrl => ctrl.Visible = false);
        
        var menuListViewItem = e.Item as MenuListViewItem;
        menuListViewItem!.AssociatedControl.Visible = true;

        if (UpdatePreviewLoaded()) {
            Clear();
        }

        Draw();
    }

    private static LayoutTree<SummaryPane> MetrePreviewLayout() =>
        LayoutTree<SummaryPane>.FromNodes([
            new LayoutNode<SummaryPane> { Id = 0, IsSplit = true, Orientation = Orientation.Column, Ratio = 0.5f, FirstId = 1, SecondId = 2 },
            new LayoutNode<SummaryPane> { Id = 1, IsSplit = true, Orientation = Orientation.Row, Ratio = 0.5f, FirstId = 3, SecondId = 4 },
            new LayoutNode<SummaryPane> { Id = 2, Pane = new(PaneControlType.CpuCores) },
            new LayoutNode<SummaryPane> { Id = 3, Pane = new(PaneControlType.Cpu) },
            new LayoutNode<SummaryPane> { Id = 4, Pane = new(PaneControlType.Memory) },
        ], rootId: 0);

    private void MetreViewOnItemClicked(object? sender, ListViewItemEventArgs e)
    {
        if (previewConfig == null || !Enum.TryParse(e.Item.SubItems[1].Text, out MetreControlStyle style)) {
            return;
        }

        previewConfig.MetreStyle = style;

        ReloadPreview();
        Preview?.Draw();
    }

    protected override void OnDraw()
    {
        Terminal.SetCursorPosition(X, Y);
        Terminal.BackgroundColor = previewTheme.MenubarBackground;
        Terminal.ForegroundColor = previewTheme.MenubarForeground;

        string menubar = Strings.SetupTitle;
        int offsetX = Terminal.WindowWidth / 2 - menubar.Length / 2;
        
        Terminal.WriteEmptyLineTo(offsetX);
        Terminal.Write(menubar);
        Terminal.WriteEmptyLineTo(Width - offsetX - menubar.Length);

        UpdateTheme();
        headerView.Draw();
        menuView.Draw();
        
        ListView activeControl = tabControls.Single(ctrl => ctrl.Visible);
        activeControl.Draw();

        Preview?.Draw();

        KeyBindControl.Draw(
            "F10",
            Strings.SetupDone,
            X,
            Height - ControlGutter,
            CommandLength,
            previewTheme,
            enabled: true,
            Terminal);

        if (messageBox.Visible) {
            messageBox.Draw();
        }
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        if (messageBox.Visible) {
            OnMessageBoxKeyPressed(keyInfo, ref handled);
            return;
        }

        base.OnKeyPressed(keyInfo, ref handled);

        if (handled) {
            return;
        }
        
        ListView activeControl  = tabControls.Single(ctrl => ctrl.Visible);
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

            case ConsoleKey.Enter when activeControl == layoutView && focusedControl == layoutView:
                OpenLayoutDesigner(layoutView.SelectedItem?.Text);
                handled = true;
                break;

            case ConsoleKey.N when activeControl == layoutView:
                OpenLayoutDesigner(null);
                handled = true;
                break;

            case ConsoleKey.F10:
                if (!SaveConfig()) {
                    handled = true;
                    ShowMessageBox(
                        Strings.CommonSaveFailedTitle,
                        runContext.AppConfig.LastSaveError ?? Strings.SetupSaveFailedText);
                }
                break;
        }
    }

    protected override void OnLoad()
    {
        Terminal.CursorVisible = false;

        CommonControl.ThemeDefaults(this, runContext.AppConfig.Theme);

        foreach (Control control in Controls) {
            control.Load();
        }

        messageBox.Load();

        foreach (ListView listView in tabControls) {
            listView.Visible = false;
        }

        LoadHeaderView();
        LoadMenuItems();
        LoadGeneralSection();
        LoadColumnsSection();
        LoadUxSection();
        
        LoadDelaySection();
        LoadLanguageSection();

        previewTheme         = runContext.AppConfig.Theme;
        preferIndexedColours = ConsolePalette.PreferIndexedColours;
        previewConfig        = runContext.AppConfig.CreatePreview();

        if (returnToLayoutsTab) {
            layoutView.Visible = true;
            SelectMenuItemFor(layoutView);
        }
        else {
            generalView.Visible = true;
            menuView.SetFocus();
        }
        
        menuView.ItemClicked  += MenuViewOnItemClicked;
        themeView.ItemClicked += ThemeViewOnItemClicked;
        metreView.ItemClicked += MetreViewOnItemClicked;
        
        base.OnLoad();
    }

    private void OnMessageBoxKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        messageBox.KeyPressed(keyInfo, ref handled);

        if (messageBox.Result == MessageBoxResult.None) {
            return;
        }

        messageBox.Visible    = false;
        Control.RedrawEnabled = true;

        Clear();
        Draw();
    }

    protected override void OnResize()
    {
        headerView.X      = X;
        headerView.Y      = Y + 2;
        headerView.Width  = Width;
        headerView.Height = 3;
        headerView.ColumnHeaders[0].Width = Math.Max(0, Width - BorderInset);

        menuView.X      = X;
        menuView.Y      = headerView.Y + headerView.Height;
        menuView.Height = Height - (headerView.Height + 4) - ControlGutter;
        menuView.Width  = MenuViewWidth;
        menuView.ColumnHeaders[0].Width = Math.Max(0, MenuViewWidth - BorderInset);

        foreach (ListView ctrl in tabControls) {
            ctrl.X      = menuView.X + menuView.Width + ControlGutter;
            ctrl.Y      = menuView.Y;
            ctrl.Height = menuView.Height;
            ctrl.Width  = Width - (menuView.Width + ControlGutter);

            int checkboxWidth = ctrl.ShowCheckboxes ? ListView.CheckboxWidth : 0;
            ctrl.ColumnHeaders[0].Width = Math.Max(0, ctrl.Width - checkboxWidth - BorderInset);
        }

        generalView.ColumnHeaders[1].Width  = 0;
        delayView.ColumnHeaders[1].Width    = 0;
        columnsView.ColumnHeaders[1].Width  = 0;
        metreView.ColumnHeaders[1].Width    = 0;
        languageView.ColumnHeaders[1].Width = 0;

        ResizePreview();

        base.OnResize();

        UpdatePreviewLoaded();
    }

    protected override void OnShown()
    {
        if (!returnToLayoutsTab) {
            return;
        }

        returnToLayoutsTab = false;
        layoutView.SetFocus();
    }

    protected override void OnUnload()
    {
        menuView.ItemClicked  -= MenuViewOnItemClicked;
        themeView.ItemClicked -= ThemeViewOnItemClicked;
        metreView.ItemClicked -= MetreViewOnItemClicked;

        Preview?.Unload();
        previewTab = null;
        previews.Clear();

        headerView.Items.Clear();
        menuView.Items.Clear();
        
        foreach (Control control in Controls) {
            control.Unload();
            (control as ListView)?.Items.Clear();
        }

        messageBox.Unload();

        Terminal.CursorVisible = true;
        
        base.OnUnload();
    }

    private void OpenLayoutDesigner(string? layoutName)
    {
        SummaryControlLayout? layout = runContext.AppConfig.Layouts.FirstOrDefault(
            l => l.Name.Equals(layoutName, StringComparison.CurrentCultureIgnoreCase));

        LayoutDesignerScreen designer = screenApp.GetScreen<LayoutDesignerScreen>();
        designer.Open(layout?.ToTree() ?? new LayoutTree<SummaryPane>(SummaryPane.Empty), layout?.Name);

        returnToLayoutsTab = true;
        screenApp.ShowScreen<LayoutDesignerScreen>();
    }

    internal SummaryControl2? Preview => previewTab != null ? previews[previewTab] : null;

    internal static Statistics PreviewProcessColumns(int innerWidth)
    {
        const int Base = ProcessControl.ColumnProcessWidth + ProcessControl.ColumnPidWidth;

        const Statistics Narrow = Statistics.Process | Statistics.Pid | Statistics.Cpu;
        const Statistics Medium = Narrow | Statistics.Mem;
        const Statistics Wide   = Medium | Statistics.User;

        return innerWidth switch {
            >= Base + ProcessControl.ColumnUserWidth + ProcessControl.ColumnCpuWidth + ProcessControl.ColumnMemoryWidth => Wide,
            >= Base + ProcessControl.ColumnCpuWidth + ProcessControl.ColumnMemoryWidth                                    => Medium,
                                                                                                                          _ => Narrow
        };
    }

    private void ReloadPreview()
    {
        if (Preview is not { } preview) {
            return;
        }

        lock (DrawingLock) {
            preview.Unload();
            preview.Load();
            preview.Resize();
        }
    }

    private void ResizePreview()
    {
        int tabWidth     = Width - (menuView.Width + ControlGutter);
        int listWidth    = tabWidth / 2;
        int previewWidth = tabWidth - listWidth - ControlGutter;

        previewFits = previewWidth >= PreviewMinWidth &&
                      menuView.Height >= PreviewMinHeight;

        if (!previewFits) {
            return;
        }

        foreach (ListView tab in previewLayouts.Keys) {
            tab.Width = listWidth;
            tab.ColumnHeaders[0].Width = Math.Max(0, listWidth - BorderInset);
        }

        if (Preview is not { } preview) {
            return;
        }

        preview.X      = menuView.X + menuView.Width + ControlGutter + listWidth + ControlGutter;
        preview.Y      = menuView.Y;
        preview.Width  = previewWidth;
        preview.Height = menuView.Height;

        foreach (ProcessControl processes in preview.PaneControls.Values.OfType<ProcessControl>()) {
            processes.VisibleColumnsOverride = PreviewProcessColumns(previewWidth - BorderInset);
        }

        preview.Resize();
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

    private void SelectMenuItemFor(ListView tab)
    {
        for (int i = 0; i < menuView.Items.Count; i++) {
            if (menuView.Items[i] is MenuListViewItem item && item.AssociatedControl == tab) {
                menuView.SelectedIndex = i;
                return;
            }
        }
    }

    private void ShowMessageBox(string title, string text)
    {
        Control.RedrawEnabled = false;

        CommonControl.Theme(messageBox, previewTheme);

        messageBox.Width = Math.Min(SaveErrorWidth, Width);
        messageBox.X     = X + Math.Max(0, (Width - messageBox.Width) / 2);
        messageBox.Y     = Y + Math.Max(0, (Height - messageBox.Height) / 2);
        messageBox.Title = title;
        messageBox.Text  = text;

        messageBox.Visible = true;
        messageBox.ShowMessageBox();
    }

    private static LayoutTree<SummaryPane> ThemePreviewLayout() =>
        LayoutTree<SummaryPane>.FromNodes([
            new LayoutNode<SummaryPane> { Id = 0, IsSplit = true, Orientation = Orientation.Column, Ratio = 0.2f, FirstId = 1, SecondId = 2 },
            new LayoutNode<SummaryPane> { Id = 1, IsSplit = true, Orientation = Orientation.Row, Ratio = 0.5f, FirstId = 3, SecondId = 4 },
            new LayoutNode<SummaryPane> { Id = 2, IsSplit = true, Orientation = Orientation.Column, Ratio = 0.4f, FirstId = 5, SecondId = 8 },
            new LayoutNode<SummaryPane> { Id = 3, Pane = new(PaneControlType.CpuMetre) },
            new LayoutNode<SummaryPane> { Id = 4, Pane = new(PaneControlType.MemoryMetre) },
            new LayoutNode<SummaryPane> { Id = 5, IsSplit = true, Orientation = Orientation.Row, Ratio = 0.5f, FirstId = 6, SecondId = 7 },
            new LayoutNode<SummaryPane> { Id = 6, Pane = new(PaneControlType.Cpu) },
            new LayoutNode<SummaryPane> { Id = 7, Pane = new(PaneControlType.Memory) },
            new LayoutNode<SummaryPane> { Id = 8, Pane = new(PaneControlType.Process, Statistics.Process | Statistics.Pid | Statistics.Cpu) },
        ], rootId: 0);

    private void ThemeViewOnItemClicked(object? sender, ListViewItemEventArgs e)
    {
        Theme theme = runContext.AppConfig.Themes
            .Where(t => t.Name.Equals(e.Item.Text, StringComparison.CurrentCultureIgnoreCase))
            .First();
        
        previewTheme = theme;

        if (previewConfig != null) {
            previewConfig.Theme = theme;
        }
        
        ConsolePalette.PreferIndexedColours = 
            TerminalCapabilities.ResolvePreferIndexed(previewTheme.ColourMode, Environment.GetEnvironmentVariable);

        ReloadPreview();
        UpdateTheme();
        Clear();
        Draw();
    }

    private bool UpdatePreviewLoaded()
    {
        ListView? wanted = previewConfig != null && previewFits
            ? previewLayouts.Keys.FirstOrDefault(tab => tab.Visible)
            : null;

        if (wanted == previewTab) {
            return false;
        }

        lock (DrawingLock) {
            Preview?.Unload();
            previewTab = wanted;

            if (wanted != null) {
                if (!previews.TryGetValue(wanted, out SummaryControl2? preview)) {
                    preview = new SummaryControl2(runContext.ServiceController, Terminal, previewConfig!, previewLayouts[wanted]());
                    previews[wanted] = preview;
                }

                preview.Load();
                ResizePreview();
            }
        }

        return true;
    }

    private void UpdateTheme()
    {
        CommonControl.ThemeDefaults(this, previewTheme);

        foreach (Control ctrl in Controls) {
            CommonControl.ThemeDefaults(ctrl, previewTheme);
        }

        CommonControl.Theme(headerView, previewTheme);
        CommonControl.Theme(menuView, previewTheme);
        
        foreach (ListView ctrl in tabControls) {
            CommonControl.Theme(ctrl, previewTheme);
        }

        layoutView.FooterText   = CommonControl.FooterText(previewTheme, ("↵", Strings.SetupLayoutsCreateEdit));
        languageView.FooterText = CommonControl.FooterNote(previewTheme, languageNote);
    }
}
