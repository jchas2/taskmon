using System.Diagnostics;
using System.Globalization;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Process;
using Task.Monitor.System;
using Task.Monitor.System.Controls.ListView;

namespace Task.Monitor.Gui.Controls;

public partial class ProcessControl
{
    public class ProcessListViewItem : ListViewItem
    {
        private int lastThreadCount;
        private long lastBasePriority;
        private long lastUsedMemory;
        private long lastDiskUsage;
        private double lastCpu;
        private double lastGpu;
        
        public ProcessListViewItem(
            ProcessorInfo processorInfo,
            ref SystemStatistics systemStatistics,
            AppConfig appConfig) 
            : base(processorInfo.FileDescription)
        {
            AppConfig = appConfig;
            Pid = processorInfo.Pid;
            AddSubItems(processorInfo);
            FormatSubItems(processorInfo, ref systemStatistics);
        }

        public int Pid { get; private set; }

        private AppConfig AppConfig { get; }

        private void AddSubItems(ProcessorInfo processorInfo)
        {
            SubItems.AddRange(
                new ListViewSubItem(this, processorInfo.Pid.ToString()),
                new ListViewSubItem(this, processorInfo.UserName),
                new ListViewSubItem(this, processorInfo.BasePriority.ToString()),
                new ListViewSubItem(this, processorInfo.CpuTimePercent.ToString("00.00%", CultureInfo.InvariantCulture)),
                new ListViewSubItem(this, processorInfo.CpuTimePercentAvg.ToString("00.00%", CultureInfo.InvariantCulture)),
                new ListViewSubItem(this, processorInfo.CpuTimePercentMax.ToString("00.00%", CultureInfo.InvariantCulture)),
                new ListViewSubItem(this, processorInfo.ThreadCount.ToString()),
                new ListViewSubItem(this, processorInfo.GpuTimePercent.ToString("00.00%", CultureInfo.InvariantCulture)),
                new ListViewSubItem(this, processorInfo.GpuTimePercentAvg.ToString("00.00%", CultureInfo.InvariantCulture)),
                new ListViewSubItem(this, processorInfo.GpuTimePercentMax.ToString("00.00%", CultureInfo.InvariantCulture)),
                new ListViewSubItem(this, processorInfo.UsedMemory.ToFormattedByteSize()),
                new ListViewSubItem(this, processorInfo.UsedMemoryAvg.ToFormattedByteSize()),
                new ListViewSubItem(this, processorInfo.UsedMemoryMax.ToFormattedByteSize()),
                new ListViewSubItem(this, processorInfo.DiskUsage.ToFormattedMbpsFromBytes()),
                new ListViewSubItem(this, processorInfo.DiskUsageAvg.ToFormattedMbpsFromBytes()),
                new ListViewSubItem(this, processorInfo.DiskUsageMax.ToFormattedMbpsFromBytes()),
                new ListViewSubItem(this, processorInfo.CmdLine));
        }

        private void FormatSubItems(ProcessorInfo processorInfo, ref SystemStatistics systemStatistics)
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
            
            if (!SubItems[(int)Columns.User].Text.Equals(Environment.UserName, StringComparison.OrdinalIgnoreCase)) {
                SubItems[(int)Columns.User].ForegroundColor = ConsolePalette.DarkGray;
            }
            
            if (AppConfig.HighlightStatisticsColumnUpdate) {
                FormatSubItem(
                    SubItems[(int)Columns.Priority],
                    () => processorInfo.BasePriority != lastBasePriority);
            }

            bool cpuHighCoreUsage = SystemInfo.GetCpuHighCoreUsage(processorInfo.CpuTimePercent);
            
            if (cpuHighCoreUsage) {
                SubItems[(int)Columns.Process].ForegroundColor = AppConfig.DefaultTheme.RangeHighBackground;
                SubItems[(int)Columns.Cpu].ForegroundColor = AppConfig.DefaultTheme.RangeHighForeground;
                SubItems[(int)Columns.Cpu].BackgroundColor = AppConfig.DefaultTheme.RangeHighBackground;
            }
            else {
                if (AppConfig.HighlightStatisticsColumnUpdate) {
                    FormatSubItem(
                        SubItems[(int)Columns.Cpu],
                        () => processorInfo.CpuTimePercent != lastCpu);
                }
            }
            
            if (AppConfig.HighlightStatisticsColumnUpdate) {
                FormatSubItem(
                    SubItems[(int)Columns.Threads],
                    () => processorInfo.ThreadCount != lastThreadCount);
            }

            if (AppConfig.HighlightStatisticsColumnUpdate) {
                FormatSubItem(
                    SubItems[(int)Columns.Gpu],
                    () => processorInfo.GpuTimePercent != lastGpu);
            }
            
            double memRatio = (double)processorInfo.UsedMemory / (double)systemStatistics.TotalPhysical;
            
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
                        () => processorInfo.UsedMemory != lastUsedMemory);
                }
            }
            
            double mbps = processorInfo.DiskUsage.ToMbpsFromBytes(); 
            
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
                        () => processorInfo.DiskUsage != lastDiskUsage);
                }
            }

            if (!processorInfo.IsDaemon && AppConfig.HighlightDaemons) {
                SubItems[(int)Columns.Process].ForegroundColor = AppConfig.DefaultTheme.ColumnCommandNormalUserSpace;
                SubItems[(int)Columns.CommandLine].ForegroundColor = AppConfig.DefaultTheme.ColumnCommandNormalUserSpace;
            }

            if (processorInfo.IsLowPriority) {
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
            
            lastCpu = processorInfo.CpuTimePercent;
            lastBasePriority = processorInfo.BasePriority;
            lastThreadCount = processorInfo.ThreadCount;
            lastGpu = processorInfo.GpuTimePercent;
            lastUsedMemory = processorInfo.UsedMemory;
            lastDiskUsage = processorInfo.DiskUsage;
        }
        
        public void UpdateSubItems(ProcessorInfo processorInfo, ref SystemStatistics systemStatistics)
        {
            Debug.Assert(processorInfo.Pid == Pid);

            SubItems[(int)Columns.Process].Text = processorInfo.FileDescription;
            SubItems[(int)Columns.Pid].Text = processorInfo.Pid.ToString();
            SubItems[(int)Columns.User].Text = processorInfo.UserName;
            SubItems[(int)Columns.Priority].Text = processorInfo.BasePriority.ToString();
            SubItems[(int)Columns.Cpu].Text = processorInfo.CpuTimePercent.ToString("00.00%", CultureInfo.InvariantCulture);
            SubItems[(int)Columns.AvgCpu].Text = processorInfo.CpuTimePercentAvg.ToString("00.00%", CultureInfo.InvariantCulture);
            SubItems[(int)Columns.MaxCpu].Text = processorInfo.CpuTimePercentMax.ToString("00.00%", CultureInfo.InvariantCulture);
            SubItems[(int)Columns.Threads].Text = processorInfo.ThreadCount.ToString();
            SubItems[(int)Columns.Gpu].Text = processorInfo.GpuTimePercent.ToString("00.00%", CultureInfo.InvariantCulture);
            SubItems[(int)Columns.AvgGpu].Text = processorInfo.GpuTimePercentAvg.ToString("00.00%", CultureInfo.InvariantCulture);
            SubItems[(int)Columns.MaxGpu].Text = processorInfo.GpuTimePercentMax.ToString("00.00%", CultureInfo.InvariantCulture);
            SubItems[(int)Columns.Memory].Text = processorInfo.UsedMemory.ToFormattedByteSize();
            SubItems[(int)Columns.AvgMemory].Text = processorInfo.UsedMemoryAvg.ToFormattedByteSize();
            SubItems[(int)Columns.MaxMemory].Text = processorInfo.UsedMemoryMax.ToFormattedByteSize();
            SubItems[(int)Columns.Disk].Text = processorInfo.DiskUsage.ToFormattedMbpsFromBytes();
            SubItems[(int)Columns.AvgDisk].Text = processorInfo.DiskUsageAvg.ToFormattedMbpsFromBytes();
            SubItems[(int)Columns.MaxDisk].Text = processorInfo.DiskUsageMax.ToFormattedMbpsFromBytes();
            
            FormatSubItems(processorInfo, ref systemStatistics);
        }
    }
}
