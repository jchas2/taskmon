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
        public const string Iterations = "iterations";

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

        // Iteration keys.
        public const string Limit = "limit";

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
        public const string HighlightDaemons = "highlight-daemons";
        public const string HighlightStatsColUpdate = "highlight-stats-col-update";
        public const string MetreStyle = "metre-style";
        public const string MultiSelectProcesses = "multi-select-procs";
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
        public const string Background = "background";
        public const string BackgroundHighlight = "background-highlight";

        public const string ColCmdNormalUserSpace = "col-cmd-normal-user-space";
        public const string ColCmdLowPriority = "col-cmd-low-priority";
        public const string ColCmdHighCpu = "col-cmd-high-cpu";
        public const string ColCmdIoBound = "col-cmd-io-bound";
        public const string ColCmdScript = "col-cmd-script";
        public const string ColUserCurrentNonRoot  = "col-user-current-non-root";
        public const string ColUserOtherNonRoot  = "col-user-other-non-root";
        public const string ColUserSystem = "col-user-system";
        public const string ColUserRoot = "col-user-root";

        public const string CommandBackground = "command-background";
        public const string CommandForeground = "command-foreground";

        public const string DeltaHighlightColour = "delta-highlight-colour";
        
        public const string Error = "error";
        public const string Foreground = "foreground";
        public const string ForegroundHighlight = "foreground-highlight";
        public const string HeaderForeground = "header-foreground";
        public const string HeaderBackground = "header-background";
        public const string MenubarForeground = "menubar-foreground";
        public const string MenubarBackground = "menubar-background";
        public const string RangeHighBackground = "range-high-background";
        public const string RangeLowBackground = "range-low-background";
        public const string RangeMidBackground = "range-mid-background";
        public const string RangeHighForeground = "range-high-foreground";
        public const string RangeLowForeground = "range-low-foreground";
        public const string RangeMidForeground = "range-mid-foreground";
        
        // Layout keys.
        public const string Ratio = "ratio";
        public const string NumRows = "num-rows";
        public const string NumCols = "num-cols";
        public const string Charts = "charts";
    }
}
