namespace Task.Monitor.Configuration;

public sealed class Constants
{
    public const string AppName = "taskmon";
    public const string ThemeDirectory = "themes";
    public const string ThemeExtension = ".theme";
    public const string LayoutDirectory = "layouts";
    public const string LayoutExtension = ".layout";
    
    public sealed class Sections
    {
        public const string Filter = "filter";
        public const string UX = "ux";
        public const string Stats = "stats";
        public const string Sort = "sort";

        public const string ThemeTaskmonDefault = "Taskmon Default";
        public const string ThemeMsDos = "MS-DOS";

        public const string LayoutAllCharts = "All Charts";
        public const string LayoutGpuAndGpuMemoryLarge = "Gpu and Gpu Memory Large";
        public const string LayoutCpuAndMemoryLarge = "Cpu and Memory Large";
    }

    public sealed class Keys
    {
        // Filter keys.
        public const string Pid = "pid";
        public const string Process = "process";
        public const string UserName = "username";

        // Sort keys.
        public const string Asc = "asc";
        public const string Col = "col";

        // Stats keys.
        public const string Cols = "cols";
        public const string Delay = "delay";
        public const string NProcs = "nprocs";

        // UX Keys.
        public const string ColourMode = "colour-mode";
        public const string ConfirmTaskDelete = "confirm-task-delete";
        public const string DefaultTheme = "default-theme";
        public const string DefaultLayout = "default-layout";
        public const string DefaultSummaryLayout2 = "default-summary-layout2";
        public const string HighlightDaemons = "highlight-daemons";
        public const string HighlightStatsColUpdate = "highlight-stats-col-update";
        public const string MetreStyle = "metre-style";
        public const string MultiSelectProcesses = "multi-select-procs";
        public const string ShowSmallMetreGrid = "show-small-metre-grid";
        public const string ShowLargeMetreGrid = "show-large-metre-grid";
        public const string ShowMetreCpuNumerically = "show-metre-cpu-numerically";
        public const string ShowMetreDiskNumerically = "show-metre-disk-numerically";
        public const string ShowMetreMemNumerically = "show-metre-mem-numerically";
        public const string ShowMetreSwapNumerically = "show-metre-swap-numerically";
        public const string ShowMetreGpuNumerically = "show-metre-gpu-numerically";
        public const string ShowMetreGpuMemNumerically = "show-metre-gpu-mem-numerically";
        public const string ShowMetreNetworkNumerically = "show-metre-network-numerically";
        public const string ShowYAxisScale = "show-y-axis-scale";
        public const string UseLargeCharts = "use-large-charts";
        public const string UseIrixCpuReporting = "use-irix-cpu-reporting";

        // Theme keys.
        public const string Background = "control.background";
        public const string BackgroundHighlight = "control.background.highlight.focused";
        public const string BackgroundHighlightInactive = "control.background.highlight.inactive";
        public const string Foreground = "control.foreground";
        public const string ForegroundHighlight = "control.foreground.highlight.focused";
        public const string ForegroundHighlightInactive = "control.foreground.highlight.inactive";
        public const string ControlBorder = "control.border";
        public const string FocusSelectionColour = "control.border.focused";

        public const string ChartBorder = "chart.border";
        public const string ChartYAxis = "chart.yaxis";
        public const string ChartTitle = "chart.title";
        public const string ChartGrid = "chart.grid";
        public const string RangeHighBackground = "chart.range.high.background";
        public const string RangeLowBackground = "chart.range.low.background";
        public const string RangeMidBackground = "chart.range.mid.background";
        public const string RangeHighForeground = "chart.range.high.foreground";
        public const string RangeLowForeground = "chart.range.low.foreground";
        public const string RangeMidForeground = "chart.range.mid.foreground";

        public const string ListViewBackground = "listview.background";
        public const string ListViewForeground = "listview.foreground";
        public const string ListViewBorder = "listview.border";
        public const string HeaderBackground = "listview.header.background";
        public const string HeaderForeground = "listview.header.foreground";

        public const string PerformancePanelBackground = "performance.panel.background";
        public const string PerformancePanelForeground = "performance.panel.foreground";
        public const string PerformancePanelTitlebarBackground = "performance.panel.titlebar.background";
        public const string PerformancePanelTitlebarForeground = "performance.panel.titlebar.foreground";
        public const string PerformanceCpuKernel = "performance.cpu.kernel";
        public const string PerformanceCpuUser = "performance.cpu.user";
        public const string PerformanceMemoryInUse = "performance.memory.inuse";
        public const string PerformanceMemoryModified = "performance.memory.modified";
        public const string PerformanceMemoryStandby = "performance.memory.standby";
        public const string PerformanceMemoryFree = "performance.memory.free";

        public const string ColCmdNormalUserSpace = "process.list.normaluserspace";
        public const string ColCmdLowPriority = "process.list.lowpriority";
        public const string ColCmdHighCpu = "process.list.highcpu";
        public const string ColCmdIoBound = "process.list.iobound";
        public const string ColCmdScript = "process.list.script";
        public const string ColUserCurrentNonRoot  = "process.list.user.currentnonroot";
        public const string ColUserOtherNonRoot  = "process.list.user.othernonroot";
        public const string ColUserSystem = "process.list.user.system";
        public const string ColUserRoot = "process.list.user.root";
        public const string DeltaHighlightColour = "process.list.deltahighlight";

        public const string HeatmapSizeSmall = "heatmap.size.small";
        public const string HeatmapSizeMid = "heatmap.size.mid";
        public const string HeatmapSizeLarge = "heatmap.size.large";
        public const string HeatmapStateScanning = "heatmap.state.scanning";
        public const string HeatmapStateCompleted = "heatmap.state.completed";
        public const string HeatmapStateFaulted = "heatmap.state.faulted";

        public const string Error = "app.error";

        public const string MenubarForeground = "menubar.foreground";
        public const string MenubarBackground = "menubar.background";

        public const string CommandBackground = "command.background";
        public const string CommandForeground = "command.foreground";
        
        // Layout keys.
        public const string Ratio = "ratio";
        public const string NumRows = "num-rows";
        public const string NumCols = "num-cols";
        public const string Charts = "charts";

        // SummaryLayout2 (recursive split-tree) keys. Shares the layouts folder and .layout
        // extension with the fixed-grid Layout format above - LayoutType is what tells the two
        // apart (see SummaryLayout2.IsTreeLayout / AppConfig.LoadLayouts).
        public const string LayoutType = "layout-type";
        public const string SummaryRoot = "root";
        public const string SummaryNodes = "nodes";
        public const string SummaryNodePrefix = "node.";
    }
}
