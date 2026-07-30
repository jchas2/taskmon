using System.Diagnostics;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Process;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using IProcessor = Task.Monitor.Process.IProcessor;
using ProcessorEventArgs = Task.Monitor.Process.ProcessorEventArgs;

namespace Task.Monitor.Gui.Controls;

public sealed partial class ProcessControl : Control
{
    private readonly IProcessor processor;
    private readonly AppConfig appConfig;
    private readonly ListView sortView;
    private readonly ListView processView;

    private List<ProcessorInfo> allProcesses = [];
    private SystemStatistics systemStatistics;
    private ControlMode mode = ControlMode.None;
    private Columns sortColumn;
    private readonly Lock allProcessesLock;
    private bool freezeUpdates = false;

    private const int SortControlWidth = 20;
    private const int ControlGutter = 1;

    private const int InvalidSelectedItemIndex = -1;

    public event EventHandler<ListViewItemEventArgs>? ProcessItemSelected;

    public ProcessControl(
        IProcessor processor,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.processor = processor;
        this.appConfig = appConfig;
        
        Statistics sortStatistic = appConfig.SortColumn;

        sortColumn = sortStatistic switch {
            Statistics.Pid => Columns.Pid,
            Statistics.Process => Columns.Process,
            Statistics.User => Columns.User,
            Statistics.Pri => Columns.Priority,
            Statistics.Cpu => Columns.Cpu,
            Statistics.AvgCpu => Columns.AvgCpu,
            Statistics.MaxCpu => Columns.MaxCpu,
            Statistics.Thrd => Columns.Threads,
            Statistics.Gpu => Columns.Gpu,
            Statistics.AvgGpu => Columns.AvgGpu,
            Statistics.MaxGpu => Columns.MaxGpu,
            Statistics.Mem => Columns.Memory,
            Statistics.AvgMem => Columns.AvgMemory,
            Statistics.MaxMem => Columns.MaxMemory,
            Statistics.Disk => Columns.Disk,
            Statistics.AvgDisk => Columns.AvgDisk,
            Statistics.MaxDisk => Columns.MaxDisk,
            Statistics.Path => Columns.CommandLine,
            _ => Columns.Cpu
        };

        allProcessesLock = new Lock();
        
        sortView = new ListView(terminal) {
            Visible = mode == ControlMode.SortSelection,
            TabStop = true,
            TabIndex = 1
        };

        sortView.ColumnHeaders.Add(new ListViewColumnHeader("SORT BY"));

        processView = new ListView(terminal) {
            Visible = true,
            TabStop = true,
            TabIndex = 2
        };

        processView.ColumnHeaders
            .Add(new ListViewColumnHeader(Columns.Process.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.Pid.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.User.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.Priority.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.Cpu.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.AvgCpu.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.MaxCpu.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.Threads.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.Gpu.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.AvgGpu.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.MaxGpu.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.Memory.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.AvgMemory.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.MaxMemory.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.Disk.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.AvgDisk.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.MaxDisk.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.CommandLine.GetTitle()));

        Controls
            .Add(sortView)
            .Add(processView);
    }

    public string FilterText { private get; set; } = string.Empty;

    private ListView? GetTargetControl()
    {
        ListView? targetControl = mode switch {
            ControlMode.None => processView,
            ControlMode.SortSelection => sortView,
            _ => null
        };

        return targetControl;
    }
    
    // Process and Pid are always shown.
    private bool IsColumnVisible(Columns column) =>
        column is Columns.Process or Columns.Pid ||
        (appConfig.VisibleColumns & ToStatistic(column)) != 0;

    private void LoadSortItems()
    {
        sortView.Items.Clear();

        IEnumerable<string> columns = Enum.GetValues<Columns>()
            .Where(c => c != Columns.Count && IsColumnVisible(c))
            .Select(c => c.GetTitle());

        foreach (var column in columns) {
            sortView.Items.Add(new ListViewItem(column));
        }
    }

    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();
            UpdateListViewItems();
            sortView.Visible = mode == ControlMode.SortSelection;
            sortView.Draw();
            processView.Draw();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)    
    {
        switch (keyInfo.Key) {
            case ConsoleKey.Escape:
                if (mode == ControlMode.SortSelection) {
                    SetMode(ControlMode.None);
                    handled = true;
                    return;
                }
                break;
            case ConsoleKey.A:
            case ConsoleKey.D:
                appConfig.SortAscending = keyInfo.Key == ConsoleKey.A;
                SortSelectionChanged(sortColumn);
                handled = true;
                return;
            case ConsoleKey.G:
                SortSelectionChanged(Columns.Gpu);
                handled = true;
                return;
            case ConsoleKey.I:
                appConfig.UseIrixReporting = !appConfig.UseIrixReporting;
                processor.IrixMode = appConfig.UseIrixReporting;
                handled = true;
                return;
            case ConsoleKey.M:
                SortSelectionChanged(Columns.Memory);
                handled = true;
                return;
            case ConsoleKey.P:
                SortSelectionChanged(Columns.Cpu);
                handled = true;
                return;
            case ConsoleKey.U:
                UncheckAllProcesses();
                handled = true;
                return;
            case ConsoleKey.X:
                appConfig.MultiSelectProcesses = !appConfig.MultiSelectProcesses;
                processView.ShowCheckboxes = appConfig.MultiSelectProcesses;
                Draw();
                handled = true;
                return;
            case ConsoleKey.F:
            case ConsoleKey.Z:
                freezeUpdates = !freezeUpdates;
                handled = true;
                return;
        }
        
        try {
            Control? targetControl = GetTargetControl();
            Control.DrawingLockAcquire();
            targetControl?.KeyPressed(keyInfo, ref handled);
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;

        ListView[] listViews = [sortView, processView];

        foreach (ListView listView in listViews) {
            listView.BackgroundHighlightColour = appConfig.DefaultTheme.BackgroundHighlight;
            listView.ForegroundHighlightColour = appConfig.DefaultTheme.ForegroundHighlight;
            listView.BackgroundColour = appConfig.DefaultTheme.Background;
            listView.ForegroundColour = appConfig.DefaultTheme.Foreground;
            listView.HeaderBackgroundColour = appConfig.DefaultTheme.HeaderBackground;
            listView.HeaderForegroundColour = appConfig.DefaultTheme.HeaderForeground;

            foreach (ListViewColumnHeader columnHeader in listView.ColumnHeaders) {
                columnHeader.BackgroundColour = appConfig.DefaultTheme.HeaderBackground;
                columnHeader.ForegroundColour = appConfig.DefaultTheme.HeaderForeground;
            }
        }
        
        UpdateColumnHeaderSort(decorate: true);
        
        processView.ShowCheckboxes = appConfig.MultiSelectProcesses;
        processView.SetFocus();

        sortView.ItemSelected += SortViewOnItemSelected;
        processView.ItemSelected += ProcessViewOnItemSelected;
        processor.ProcessorUpdated += ProcessorOnProcessorUpdated;

        LoadSortItems();
        
        base.OnLoad();
    }

    protected override void OnResize()
    {
        sortView.X = X;
        sortView.Y = Y;
        sortView.Width = SortControlWidth;
        sortView.Height = Height;
        sortView.ColumnHeaders[0].Width = SortControlWidth;

        int pX = X;
        int pWidth = Width;

        if (mode == ControlMode.SortSelection) {
            pX = sortView.X + sortView.Width + ControlGutter;
            pWidth = Width - (sortView.Width + ControlGutter);
        }

        processView.X = pX;
        processView.Y = Y;
        processView.Width = pWidth;
        processView.Height = Height;
        
        int SetWidth(Columns column, int width, bool rightAligned = false)
        {
            int effectiveWidth = IsColumnVisible(column) ? width : 0;
            processView.ColumnHeaders[(int)column].Width = effectiveWidth;
            processView.ColumnHeaders[(int)column].RightAligned = rightAligned;
            return effectiveWidth;
        }

        int total =
            SetWidth(Columns.Process, ColumnProcessWidth) +
            SetWidth(Columns.Pid, ColumnPidWidth) +
            SetWidth(Columns.User, ColumnUserWidth) +
            SetWidth(Columns.Priority, ColumnPriorityWidth, rightAligned: true) +
            SetWidth(Columns.Cpu, ColumnCpuWidth, rightAligned: true) +
            SetWidth(Columns.AvgCpu, ColumnAvgCpuWidth, rightAligned: true) +
            SetWidth(Columns.MaxCpu, ColumnMaxCpuWidth, rightAligned: true) +
            SetWidth(Columns.Threads, ColumnThreadsWidth, rightAligned: true) +
            SetWidth(Columns.Gpu, ColumnGpuWidth, rightAligned: true) +
            SetWidth(Columns.AvgGpu, ColumnAvgGpuWidth, rightAligned: true) +
            SetWidth(Columns.MaxGpu, ColumnMaxGpuWidth, rightAligned: true) +
            SetWidth(Columns.Memory, ColumnMemoryWidth, rightAligned: true) +
            SetWidth(Columns.AvgMemory, ColumnAvgMemoryWidth, rightAligned: true) +
            SetWidth(Columns.MaxMemory, ColumnMaxMemoryWidth, rightAligned: true) +
            SetWidth(Columns.Disk, ColumnDiskWidth, rightAligned: true) +
            SetWidth(Columns.AvgDisk, ColumnAvgDiskWidth, rightAligned: true) +
            SetWidth(Columns.MaxDisk, ColumnMaxDiskWidth, rightAligned: true);

        int processViewWidth =
            processView.ShowCheckboxes ? processView.Width - ListView.CheckboxWidth : processView.Width;

        int commandLineWidth = total + ColumnCommandlineWidth < processViewWidth
            ? processViewWidth - total
            : ColumnCommandlineWidth;

        processView.ColumnHeaders[(int)Columns.CommandLine].Width =
            IsColumnVisible(Columns.CommandLine) ? commandLineWidth : 0;

        base.OnResize();
    }

    protected override void OnUnload()
    {
        sortView.ItemSelected -= SortViewOnItemSelected;
        processView.ItemSelected -= ProcessViewOnItemSelected;
        processor.ProcessorUpdated -= ProcessorOnProcessorUpdated;
        
        base.OnUnload();
    }

    private void ProcessorOnProcessorUpdated(object? sender, ProcessorEventArgs e)
    {
        if (freezeUpdates) {
            return;
        }
        
        lock (allProcessesLock) {
            allProcesses = e.ProcessInfos;
        }

        systemStatistics = e.Statistics;
        Draw();
    }

    private void ProcessViewOnItemSelected(object? sender, ListViewItemEventArgs e) =>
        ProcessItemSelected?.Invoke(sender, e);

    public List<int> CheckedProcesses
    {
        get {
            try {
                Control.DrawingLockAcquire();

                int GetItemPid(ListViewItem item)
                {
                    ListViewSubItem selectedSubItem = item.SubItems[(int)Columns.Pid];

                    if (int.TryParse(selectedSubItem.Text, out int pid)) {
                        return pid;
                    }

                    return InvalidSelectedItemIndex;
                }

                List<int> checkedProcesses = processView.ShowCheckboxes
                    ? processView.Items
                        .Where(item => item.Checked)
                        .Select(item => GetItemPid(item))
                        .ToList()
                    : [];

                return checkedProcesses;
            }
            finally {
                Control.DrawingLockRelease();
            }
        }
    }

    public int SelectedProcessId
    {
        get {
            try {
                Control.DrawingLockAcquire();
                if (processView.SelectedItem == null) {
                    return InvalidSelectedItemIndex;
                }

                ListViewSubItem selectedSubItem = processView.SelectedItem.SubItems[(int)Columns.Pid];

                if (int.TryParse(selectedSubItem.Text, out int pid)) {
                    return pid;
                }

                return InvalidSelectedItemIndex;
            }
            finally {
                Control.DrawingLockRelease();
            }
        }
    }

    public void SetMode(ControlMode mode)
    {
        if (mode == this.mode) {
            return;
        }

        this.mode = mode;
        sortView.Visible = this.mode == ControlMode.SortSelection;

        if (sortView.Visible) {
            LoadSortItems();
        }

        Control? targetControl = GetTargetControl();
        targetControl?.SetFocus();

        Clear();
        Resize();
        Draw();
    }

    private void SortSelectionChanged(Columns column)
    {
        UpdateColumnHeaderSort(decorate: false);
        sortColumn = column;
        UpdateColumnHeaderSort(decorate: true);
        
        appConfig.SortColumn = ToStatistic(sortColumn);
        Trace.WriteLine($"Sort selection = {sortColumn}.");
        
        mode = ControlMode.None;
        processView.SetFocus();

        Clear();
        Resize();
        Draw();
    }

    private void SortViewOnItemSelected(object? sender, ListViewItemEventArgs e)
    {
        Columns column = Enum.GetValues<Columns>().Single(c => c.GetTitle() == e.Item.Text);
        SortSelectionChanged(column);        
    }
    
    private static Statistics ToStatistic(Columns column) => column switch {
        Columns.Process => Statistics.Process,
        Columns.Pid => Statistics.Pid,
        Columns.User => Statistics.User,
        Columns.Priority => Statistics.Pri,
        Columns.Cpu => Statistics.Cpu,
        Columns.AvgCpu => Statistics.AvgCpu,
        Columns.MaxCpu => Statistics.MaxCpu,
        Columns.Threads => Statistics.Thrd,
        Columns.Gpu => Statistics.Gpu,
        Columns.AvgGpu => Statistics.AvgGpu,
        Columns.MaxGpu => Statistics.MaxGpu,
        Columns.Memory => Statistics.Mem,
        Columns.AvgMemory => Statistics.AvgMem,
        Columns.MaxMemory => Statistics.MaxMem,
        Columns.Disk => Statistics.Disk,
        Columns.AvgDisk => Statistics.AvgDisk,
        Columns.MaxDisk => Statistics.MaxDisk,
        Columns.CommandLine => Statistics.Path,
        _ => default
    };

    private void UncheckAllProcesses()
    {
        try {
            Control.DrawingLockAcquire();
            IEnumerable<ListViewItem> checkedItems = processView.Items.Where(item => item.Checked);
            
            foreach (ListViewItem item in checkedItems) {
                item.Checked = false;
            }
            
            processView.Draw();
        }
        finally {
            Control.DrawingLockRelease();
        }
        
    }
    
    private void UpdateColumnHeaderSort(bool decorate)
    {

        if (!decorate) {
            processView.ColumnHeaders[(int)sortColumn].BackgroundColour = appConfig.DefaultTheme.HeaderBackground;
            processView.ColumnHeaders[(int)sortColumn].ForegroundColour = appConfig.DefaultTheme.HeaderForeground;
            
            processView.ColumnHeaders[(int)sortColumn].Text = sortColumn.GetTitle();
        }
        else {
            processView.ColumnHeaders[(int)sortColumn].BackgroundColour = appConfig.DefaultTheme.BackgroundHighlight;
            processView.ColumnHeaders[(int)sortColumn].ForegroundColour = appConfig.DefaultTheme.ForegroundHighlight;
            
            processView.ColumnHeaders[(int)sortColumn].Text = appConfig.SortAscending
                ? sortColumn.GetTitle() + "\u2191"
                : sortColumn.GetTitle() + "\u2193";
        }
    }

    private void UpdateListViewItems()
    {
        lock (allProcessesLock) {
            IEnumerable<ProcessorInfo> filteredProcesses = allProcesses;

            if (appConfig.FilterPid > -1) {
                filteredProcesses = filteredProcesses
                    .Where(p => p.Pid == appConfig.FilterPid);
            }
            else if (!string.IsNullOrWhiteSpace(appConfig.FilterUserName)) {
                filteredProcesses = filteredProcesses
                    .Where(p => p.UserName.Contains(appConfig.FilterUserName, StringComparison.OrdinalIgnoreCase));
            }
            else if (!string.IsNullOrWhiteSpace(appConfig.FilterProcess)) {
                filteredProcesses = filteredProcesses
                    .Where(p => p.ProcessName.Contains(appConfig.FilterProcess, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(FilterText)) {
                filteredProcesses = filteredProcesses
                    .Where(p => p.ProcessName.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase) ||
                                p.FileDescription.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase) ||
                                p.CmdLine.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase) ||
                                p.UserName.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase));
            }

            IOrderedEnumerable<ProcessorInfo> Sort<TKey>(Func<ProcessorInfo, TKey> key) =>
                appConfig.SortAscending
                    ? filteredProcesses.OrderBy(key)
                    : filteredProcesses.OrderByDescending(key);

            List<ProcessorInfo> sortedProcesses = (sortColumn switch {
                Columns.Cpu => Sort(p => p.CpuTimePercent),
                Columns.AvgCpu => Sort(p => p.CpuTimePercentAvg),
                Columns.MaxCpu => Sort(p => p.CpuTimePercentMax),
                Columns.Disk => Sort(p => p.DiskUsage),
                Columns.AvgDisk => Sort(p => p.DiskUsageAvg),
                Columns.MaxDisk => Sort(p => p.DiskUsageMax),
                Columns.Memory => Sort(p => p.UsedMemory),
                Columns.AvgMemory => Sort(p => p.UsedMemoryAvg),
                Columns.MaxMemory => Sort(p => p.UsedMemoryMax),
                Columns.Pid => Sort(p => p.Pid),
                Columns.Priority => Sort(p => p.BasePriority),
                Columns.Process => Sort(p => p.FileDescription),
                Columns.Threads => Sort(p => p.ThreadCount),
                Columns.Gpu => Sort(p => p.GpuTimePercent),
                Columns.AvgGpu => Sort(p => p.GpuTimePercentAvg),
                Columns.MaxGpu => Sort(p => p.GpuTimePercentMax),
                Columns.User => Sort(p => p.UserName),
                Columns.CommandLine => Sort(p => p.CmdLine),
                _ => filteredProcesses.OrderByDescending(p => p.CpuTimePercent)
            }).ToList();

            if (appConfig.NumberOfProcesses > -1) {
                sortedProcesses = sortedProcesses
                    .Take(appConfig.NumberOfProcesses)
                    .ToList();
            }

            if (sortedProcesses.Count == 0) {
                processView.Items.Clear();
                return;
            }

            int selectedIndex = processView.SelectedIndex;

            HashSet<int> sortedPids = new(sortedProcesses.Count);

            for (int i = 0; i < sortedProcesses.Count; i++) {
                sortedPids.Add(sortedProcesses[i].Pid);
            }

            for (int i = processView.Items.Count - 1; i >= 0; i--) {
                var item = (ProcessListViewItem)processView.Items[i];

                if (!sortedPids.Contains(item.Pid)) {
                    processView.Items.RemoveAt(i);
                }
            }

            var processLookup = processView.Items.Cast<ProcessListViewItem>().ToDictionary(p => p.Pid);

            for (int i = 0; i < sortedProcesses.Count; i++) {
                if (processLookup.TryGetValue(sortedProcesses[i].Pid, out var foundItem)) {
                    foundItem.UpdateSubItems(sortedProcesses[i], ref systemStatistics);
                    int insertAt = Math.Min(i, processView.Items.Count - 1);
                    processView.Items.Remove(foundItem);
                    processView.Items.InsertAt(insertAt, foundItem);
                }
                else {
                    ProcessListViewItem item = new(sortedProcesses[i], ref systemStatistics, appConfig);
                    processView.Items.InsertAt(i, item);
                }
            }

            if (processView.Items.Count > 0) {
                processView.SelectedIndex = selectedIndex >= 0 && selectedIndex < processView.Items.Count
                    ? selectedIndex
                    : 0;
            }
        }
    }
}
