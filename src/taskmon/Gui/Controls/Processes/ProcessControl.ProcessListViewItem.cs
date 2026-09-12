using System.Diagnostics;
using System.Globalization;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.Gui.Controls.Processes;

public partial class ProcessControl
{
    public class ProcessListViewItem : ListViewItem
    {
        private int lastThreadCount;
        private long lastBasePriority;
        private long lastUsedMemory;
        private double lastDiskBytesPerSecond;
        private double lastCpu;
        private double lastGpu;
        private static string currThreadUser = string.Empty;

        private static string[] metre = [
            "⣿         ",
            "⣿⣿        ",
            "⣿⣿⣿⣿      ",
            "⣿⣿⣿⣿⣿⣿⣿   ",
            "⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿"
        ];
                                        
        
        public ProcessListViewItem(
            ProcessEntry processEntry,
            ulong totalPhysicalMemory,
            AppConfig appConfig) 
            : base(processEntry.FileDescription)
        {
            AppConfig = appConfig;
            Pid = processEntry.Pid;
            AddSubItems(processEntry);
            FormatSubItems(processEntry, totalPhysicalMemory);
            currThreadUser = Environment.UserName;
        }

        public int Pid { get; private set; }

        private AppConfig AppConfig { get; }

        private static string FormatPowerBucket(ProcessPowerBucket bucket) => bucket switch {
            ProcessPowerBucket.VeryLow  => metre[(int)ProcessPowerBucket.VeryLow],
            ProcessPowerBucket.Low      => metre[(int)ProcessPowerBucket.Low],
            ProcessPowerBucket.Moderate => metre[(int)ProcessPowerBucket.Moderate],
            ProcessPowerBucket.High     => metre[(int)ProcessPowerBucket.High],
            ProcessPowerBucket.VeryHigh => metre[(int)ProcessPowerBucket.VeryHigh],
                                      _ => metre[(int)ProcessPowerBucket.VeryLow]
        };

        private void AddSubItems(ProcessEntry processEntry)
        {
            SubItems.AddRange(
                new ListViewSubItem(this, processEntry.Pid.ToString()),
                new ListViewSubItem(this, processEntry.UserName),
                new ListViewSubItem(this, processEntry.BasePriority.ToString()),
                new ListViewSubItem(this, processEntry.CpuTimePercent.ToString("00.00%", CultureInfo.InvariantCulture)),
                new ListViewSubItem(this, processEntry.CpuTimePercentAvg.ToString("00.00%", CultureInfo.InvariantCulture)),
                new ListViewSubItem(this, processEntry.CpuTimePercentMax.ToString("00.00%", CultureInfo.InvariantCulture)),
                new ListViewSubItem(this, processEntry.ThreadCount.ToString()),
                new ListViewSubItem(this, processEntry.GpuTimePercent.ToString("00.00%", CultureInfo.InvariantCulture)),
                new ListViewSubItem(this, processEntry.GpuTimePercentAvg.ToString("00.00%", CultureInfo.InvariantCulture)),
                new ListViewSubItem(this, processEntry.GpuTimePercentMax.ToString("00.00%", CultureInfo.InvariantCulture)),
                new ListViewSubItem(this, processEntry.UsedMemory.ToFormattedByteSize()),
                new ListViewSubItem(this, processEntry.UsedMemoryAvg.ToFormattedByteSize()),
                new ListViewSubItem(this, processEntry.UsedMemoryMax.ToFormattedByteSize()),
                new ListViewSubItem(this, processEntry.DiskBytesPerSecond.ToFormattedMbpsFromBytes()),
                new ListViewSubItem(this, processEntry.DiskBytesPerSecondAvg.ToFormattedMbpsFromBytes()),
                new ListViewSubItem(this, processEntry.DiskBytesPerSecondMax.ToFormattedMbpsFromBytes()),
                new ListViewSubItem(this, FormatPowerBucket(processEntry.PowerBucket)),
                new ListViewSubItem(this, processEntry.CmdLine));
        }

        private void FormatSubItems(ProcessEntry processEntry, ulong totalPhysicalMemory)
        {
            void FormatSubItem(ListViewSubItem subItem, Func<bool> condition)
            {
                if (condition.Invoke()) {
                    subItem.ForegroundColor = AppConfig.DefaultTheme.DeltaHighlightColour;
                }
            }
            
            for (int i = 0; i < (int)Columns.Count; i++) {
                SubItems[i].BackgroundColor = AppConfig.DefaultTheme.Background;
                SubItems[i].ForegroundColor = AppConfig.DefaultTheme.Foreground;
            }
            
            if (!processEntry.IsRunningAsRoot) {
                SubItems[(int)Columns.User].ForegroundColor = SubItems[(int)Columns.User].Text.Equals(currThreadUser, StringComparison.OrdinalIgnoreCase)
                    ? SubItems[(int)Columns.User].ForegroundColor = AppConfig.DefaultTheme.ColumnUserCurrentNonRoot
                    : SubItems[(int)Columns.User].ForegroundColor = AppConfig.DefaultTheme.ColumnUserOtherNonRoot; 
            }
            else {
                SubItems[(int)Columns.User].ForegroundColor = AppConfig.DefaultTheme.ColumnUserRoot;
            }
            
            if (AppConfig.HighlightStatisticsColumnUpdate) {
                FormatSubItem(
                    SubItems[(int)Columns.Priority],
                    () => processEntry.BasePriority != lastBasePriority);
            }

            bool cpuHighCoreUsage = SystemInfo.GetCpuHighCoreUsage(processEntry.CpuTimePercent);
            
            if (cpuHighCoreUsage) {
                SubItems[(int)Columns.Process].ForegroundColor = AppConfig.DefaultTheme.RangeHighBackground;
                SubItems[(int)Columns.Cpu].ForegroundColor = AppConfig.DefaultTheme.RangeHighForeground;
                SubItems[(int)Columns.Cpu].BackgroundColor = AppConfig.DefaultTheme.RangeHighBackground;
            }
            else {
                if (AppConfig.HighlightStatisticsColumnUpdate) {
                    FormatSubItem(
                        SubItems[(int)Columns.Cpu],
                        () => processEntry.CpuTimePercent != lastCpu);
                }
            }
            
            if (AppConfig.HighlightStatisticsColumnUpdate) {
                FormatSubItem(
                    SubItems[(int)Columns.Threads],
                    () => processEntry.ThreadCount != lastThreadCount);
            }

            if (AppConfig.HighlightStatisticsColumnUpdate) {
                FormatSubItem(
                    SubItems[(int)Columns.Gpu],
                    () => processEntry.GpuTimePercent != lastGpu);
            }
            
            // Zero until the memory service has published. Guarded so the first frame does not
            // divide by it and paint every row as though it were using all the memory on the box.
            double memRatio = totalPhysicalMemory > 0
                ? processEntry.UsedMemory / (double)totalPhysicalMemory
                : 0.0;
            
            if (memRatio > 0.1 && memRatio <= 0.2) {
                SubItems[(int)Columns.Memory].ForegroundColor = AppConfig.DefaultTheme.RangeLowForeground;
                SubItems[(int)Columns.Memory].BackgroundColor = AppConfig.DefaultTheme.RangeLowBackground;
            }
            else if (memRatio > 0.2 && memRatio <= 0.5) {
                SubItems[(int)Columns.Memory].ForegroundColor = AppConfig.DefaultTheme.RangeMidForeground;
                SubItems[(int)Columns.Memory].BackgroundColor = AppConfig.DefaultTheme.RangeMidBackground;
            }
            else if (memRatio > 0.5) {
                SubItems[(int)Columns.Memory].ForegroundColor = AppConfig.DefaultTheme.RangeHighForeground;
                SubItems[(int)Columns.Memory].BackgroundColor = AppConfig.DefaultTheme.RangeHighBackground;
            }
            else {
                if (AppConfig.HighlightStatisticsColumnUpdate) {
                    FormatSubItem(
                        SubItems[(int)Columns.Memory],
                        () => processEntry.UsedMemory != lastUsedMemory);
                }
            }
            
            double mbps = processEntry.DiskBytesPerSecond.ToMbpsFromBytes(); 
            
            if (mbps > 1.0) {
                if (mbps < 10.0) {
                    SubItems[(int)Columns.Disk].ForegroundColor = AppConfig.DefaultTheme.RangeLowForeground;
                    SubItems[(int)Columns.Disk].BackgroundColor = AppConfig.DefaultTheme.RangeLowBackground;
                }
                else if (mbps < 100.0) {
                    SubItems[(int)Columns.Disk].ForegroundColor = AppConfig.DefaultTheme.RangeMidForeground;
                    SubItems[(int)Columns.Disk].BackgroundColor = AppConfig.DefaultTheme.RangeMidBackground;
                }
                else {
                    SubItems[(int)Columns.Disk].ForegroundColor = AppConfig.DefaultTheme.RangeHighForeground;
                    SubItems[(int)Columns.Disk].BackgroundColor = AppConfig.DefaultTheme.RangeHighBackground;
                }
            }
            else {
                if (AppConfig.HighlightStatisticsColumnUpdate) {
                    FormatSubItem(
                        SubItems[(int)Columns.Disk],
                        () => processEntry.DiskBytesPerSecond != lastDiskBytesPerSecond);
                }
            }

            if (!processEntry.IsDaemon && AppConfig.HighlightDaemons) {
                SubItems[(int)Columns.Process].ForegroundColor = AppConfig.DefaultTheme.ColumnCommandNormalUserSpace;
                SubItems[(int)Columns.CommandLine].ForegroundColor = AppConfig.DefaultTheme.ColumnCommandNormalUserSpace;
            }

            if (processEntry.IsLowPriority) {
                SubItems[(int)Columns.Process].ForegroundColor = AppConfig.DefaultTheme.ColumnCommandLowPriority;
                SubItems[(int)Columns.CommandLine].ForegroundColor = AppConfig.DefaultTheme.ColumnCommandLowPriority;
            }

            if (mbps >= 100.0) {
                SubItems[(int)Columns.Process].ForegroundColor = AppConfig.DefaultTheme.ColumnCommandIoBound;
                SubItems[(int)Columns.CommandLine].ForegroundColor = AppConfig.DefaultTheme.ColumnCommandIoBound;
            }

            if (cpuHighCoreUsage) {
                SubItems[(int)Columns.Process].ForegroundColor = AppConfig.DefaultTheme.ColumnCommandHighCpu;
                SubItems[(int)Columns.CommandLine].ForegroundColor = AppConfig.DefaultTheme.ColumnCommandHighCpu;
            }

            SubItems[(int)Columns.Power].BackgroundColor = AppConfig.DefaultTheme.Background;

            if (processEntry.PowerBucket == ProcessPowerBucket.VeryLow ||
                processEntry.PowerBucket == ProcessPowerBucket.Low) {
                SubItems[(int)Columns.Power].ForegroundColor = AppConfig.DefaultTheme.RangeLowBackground;
            }
            else if (processEntry.PowerBucket == ProcessPowerBucket.Moderate) {
                SubItems[(int)Columns.Power].ForegroundColor = AppConfig.DefaultTheme.RangeMidBackground;
            }
            else if (processEntry.PowerBucket == ProcessPowerBucket.High ||
                processEntry.PowerBucket == ProcessPowerBucket.VeryHigh) {
                SubItems[(int)Columns.Power].ForegroundColor = AppConfig.DefaultTheme.RangeHighBackground;
            }
            
            lastCpu = processEntry.CpuTimePercent;
            lastBasePriority = processEntry.BasePriority;
            lastThreadCount = processEntry.ThreadCount;
            lastGpu = processEntry.GpuTimePercent;
            lastUsedMemory = processEntry.UsedMemory;
            lastDiskBytesPerSecond = processEntry.DiskBytesPerSecond;
        }
        
        public void UpdateSubItems(ProcessEntry processEntry, ulong totalPhysicalMemory)
        {
            Debug.Assert(processEntry.Pid == Pid);

            SubItems[(int)Columns.Process].Text = processEntry.FileDescription;
            SubItems[(int)Columns.Pid].Text = processEntry.Pid.ToString();
            SubItems[(int)Columns.User].Text = processEntry.UserName;
            SubItems[(int)Columns.Priority].Text = processEntry.BasePriority.ToString();
            SubItems[(int)Columns.Cpu].Text = processEntry.CpuTimePercent.ToString("00.00%", CultureInfo.InvariantCulture);
            SubItems[(int)Columns.AvgCpu].Text = processEntry.CpuTimePercentAvg.ToString("00.00%", CultureInfo.InvariantCulture);
            SubItems[(int)Columns.MaxCpu].Text = processEntry.CpuTimePercentMax.ToString("00.00%", CultureInfo.InvariantCulture);
            SubItems[(int)Columns.Threads].Text = processEntry.ThreadCount.ToString();
            SubItems[(int)Columns.Gpu].Text = processEntry.GpuTimePercent.ToString("00.00%", CultureInfo.InvariantCulture);
            SubItems[(int)Columns.AvgGpu].Text = processEntry.GpuTimePercentAvg.ToString("00.00%", CultureInfo.InvariantCulture);
            SubItems[(int)Columns.MaxGpu].Text = processEntry.GpuTimePercentMax.ToString("00.00%", CultureInfo.InvariantCulture);
            SubItems[(int)Columns.Memory].Text = processEntry.UsedMemory.ToFormattedByteSize();
            SubItems[(int)Columns.AvgMemory].Text = processEntry.UsedMemoryAvg.ToFormattedByteSize();
            SubItems[(int)Columns.MaxMemory].Text = processEntry.UsedMemoryMax.ToFormattedByteSize();
            SubItems[(int)Columns.Disk].Text = processEntry.DiskBytesPerSecond.ToFormattedMbpsFromBytes();
            SubItems[(int)Columns.AvgDisk].Text = processEntry.DiskBytesPerSecondAvg.ToFormattedMbpsFromBytes();
            SubItems[(int)Columns.MaxDisk].Text = processEntry.DiskBytesPerSecondMax.ToFormattedMbpsFromBytes();
            SubItems[(int)Columns.Power].Text = FormatPowerBucket(processEntry.PowerBucket);

            FormatSubItems(processEntry, totalPhysicalMemory);
        }
    }
}
