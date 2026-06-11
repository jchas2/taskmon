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
    private class CmdLineFilters
    {
        public int Pid { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string Process { get; init; } = string.Empty;
        public int NumProcs { get; init; }
    }

    private readonly IProcessor processor;
    private readonly AppConfig appConfig;
    private readonly ListView sortView;
    private readonly ListView processView;
    private readonly CmdLineFilters cmdLineFilters;

    private List<ProcessorInfo> allProcesses = [];
    private SystemStatistics systemStatistics;
    private ControlMode mode = ControlMode.None;
    private Columns sortColumn;
    private readonly Lock allProcessesLock;
    private bool sortAscending = false;

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

        cmdLineFilters = new CmdLineFilters {
            Pid = appConfig.FilterPid,
            UserName = appConfig.FilterUserName,
            Process = appConfig.FilterProcess,
            NumProcs = appConfig.NumberOfProcesses
        };

        Statistics sortStatistic = appConfig.SortColumn;

        sortColumn = sortStatistic switch {
            Statistics.Pid => Columns.Pid,
            Statistics.Process => Columns.Process,
            Statistics.User => Columns.User,
            Statistics.Pri => Columns.Priority,
            Statistics.Cpu => Columns.Cpu,
            Statistics.Thrd => Columns.Threads,
            Statistics.Gpu => Columns.Gpu,
            Statistics.Mem => Columns.Memory,
            Statistics.Disk => Columns.Disk,
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
            .Add(new ListViewColumnHeader(Columns.Threads.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.Gpu.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.Memory.GetTitle()))
            .Add(new ListViewColumnHeader(Columns.Disk.GetTitle()))
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

    private void LoadSortItems()
    {
        IEnumerable<string> columns = Enum.GetValues<Columns>()
            .Where(c => c != Columns.Count)
            .Select(c => c.GetTitle());

        foreach (var column in columns) {
            sortView.Items.Add(new ListViewItem(column));
        }
    }

    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();

            if (sortView.Items.Count == 0) {
                LoadSortItems();
            }

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
        if (keyInfo.Key == ConsoleKey.Escape && mode == ControlMode.SortSelection) {
            SetMode(ControlMode.None);
            handled = true;
            return;
        }

        if (keyInfo.Key == ConsoleKey.A || keyInfo.Key == ConsoleKey.D) {
            sortAscending = keyInfo.Key == ConsoleKey.A;
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
        base.OnLoad();

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

        processView.ColumnHeaders[(int)sortColumn].BackgroundColour = appConfig.DefaultTheme.BackgroundHighlight;
        processView.ColumnHeaders[(int)sortColumn].ForegroundColour = appConfig.DefaultTheme.ForegroundHighlight;
        processView.ShowCheckboxes = appConfig.MultiSelectProcesses;
        processView.SetFocus();

        sortView.ItemSelected += SortViewOnItemSelected;
        processView.ItemSelected += ProcessViewOnItemSelected;
        processor.ProcessorUpdated += ProcessorOnProcessorUpdated;
    }

    protected override void OnResize()
    {
        sortView.X = X;
        sortView.Y = Y;
        sortView.Width = SortControlWidth;
        sortView.Height = Height;
        sortView.ColumnHeaders[0].Width = SortControlWidth;
        sortView.Resize();

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

        processView.ColumnHeaders[(int)Columns.Process].Width = ColumnProcessWidth;
        processView.ColumnHeaders[(int)Columns.Pid].Width = ColumnPidWidth;
        processView.ColumnHeaders[(int)Columns.User].Width = ColumnUserWidth;
        processView.ColumnHeaders[(int)Columns.Priority].Width = ColumnPriorityWidth;
        processView.ColumnHeaders[(int)Columns.Priority].RightAligned = true;
        processView.ColumnHeaders[(int)Columns.Cpu].Width = ColumnCpuWidth;
        processView.ColumnHeaders[(int)Columns.Cpu].RightAligned = true;
        processView.ColumnHeaders[(int)Columns.Threads].Width = ColumnThreadsWidth;
        processView.ColumnHeaders[(int)Columns.Threads].RightAligned = true;
        processView.ColumnHeaders[(int)Columns.Gpu].Width = ColumnGpuWidth;
        processView.ColumnHeaders[(int)Columns.Gpu].RightAligned = true;
        processView.ColumnHeaders[(int)Columns.Memory].Width = ColumnMemoryWidth;
        processView.ColumnHeaders[(int)Columns.Memory].RightAligned = true;
        processView.ColumnHeaders[(int)Columns.Disk].Width = ColumnDiskWidth;
        processView.ColumnHeaders[(int)Columns.Disk].RightAligned = true;

        int total =
            ColumnProcessWidth +
            ColumnPidWidth +
            ColumnUserWidth +
            ColumnPriorityWidth +
            ColumnCpuWidth +
            ColumnThreadsWidth +
            ColumnGpuWidth +
            ColumnMemoryWidth +
            ColumnDiskWidth;

        int processViewWidth =
            processView.ShowCheckboxes ? processView.Width - ListView.CheckboxWidth : processView.Width;

        processView.ColumnHeaders[(int)Columns.CommandLine].Width = total + ColumnCommandlineWidth < processViewWidth
            ? processView.ColumnHeaders[(int)Columns.CommandLine].Width = processViewWidth - total
            : processView.ColumnHeaders[(int)Columns.CommandLine].Width = ColumnCommandlineWidth;

        processView.Resize();
    }

    protected override void OnUnload()
    {
        base.OnUnload();

        sortView.ItemSelected -= SortViewOnItemSelected;
        processView.ItemSelected -= ProcessViewOnItemSelected;
        processor.ProcessorUpdated -= ProcessorOnProcessorUpdated;
    }

    private void ProcessorOnProcessorUpdated(object? sender, ProcessorEventArgs e)
    {
        lock (allProcessesLock) {
            allProcesses = e.ProcessInfos;
        }

        systemStatistics = e.SystemStatistics;

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

        Control? targetControl = GetTargetControl();
        targetControl?.SetFocus();

        Clear();
        Resize();
        Draw();
    }

    private void SortViewOnItemSelected(object? sender, ListViewItemEventArgs e)
    {
        processView.ColumnHeaders[(int)sortColumn].BackgroundColour = appConfig.DefaultTheme.HeaderBackground;
        processView.ColumnHeaders[(int)sortColumn].ForegroundColour = appConfig.DefaultTheme.HeaderForeground;

        sortColumn = Enum.GetValues<Columns>().Single(c => c.GetTitle() == e.Item.Text);

        processView.ColumnHeaders[(int)sortColumn].BackgroundColour = appConfig.DefaultTheme.BackgroundHighlight;
        processView.ColumnHeaders[(int)sortColumn].ForegroundColour = appConfig.DefaultTheme.ForegroundHighlight;

        mode = ControlMode.None;
        processView.SetFocus();

        Clear();
        Resize();
        Draw();
    }

    private void UpdateListViewItems()
    {
        lock (allProcessesLock) {
            IEnumerable<ProcessorInfo> filteredProcesses = allProcesses;

            if (cmdLineFilters.Pid > -1) {
                filteredProcesses = filteredProcesses
                    .Where(p => p.Pid == cmdLineFilters.Pid);
            }
            else if (!string.IsNullOrWhiteSpace(cmdLineFilters.UserName)) {
                filteredProcesses = filteredProcesses
                    .Where(p => p.UserName.Contains(cmdLineFilters.UserName, StringComparison.OrdinalIgnoreCase));
            }
            else if (!string.IsNullOrWhiteSpace(cmdLineFilters.Process)) {
                filteredProcesses = filteredProcesses
                    .Where(p => p.ProcessName.Contains(cmdLineFilters.Process, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(FilterText)) {
                filteredProcesses = filteredProcesses
                    .Where(p => p.ProcessName.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase) ||
                                p.FileDescription.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase) ||
                                p.CmdLine.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase) ||
                                p.UserName.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase));
            }

            IOrderedEnumerable<ProcessorInfo> Sort<TKey>(Func<ProcessorInfo, TKey> key) =>
                sortAscending
                    ? filteredProcesses.OrderBy(key)
                    : filteredProcesses.OrderByDescending(key);

            List<ProcessorInfo> sortedProcesses = (sortColumn switch {
                Columns.Cpu => Sort(p => p.CpuTimePercent),
                Columns.Disk => Sort(p => p.DiskUsage),
                Columns.Memory => Sort(p => p.UsedMemory),
                Columns.Pid => Sort(p => p.Pid),
                Columns.Priority => Sort(p => p.BasePriority),
                Columns.Process => Sort(p => p.FileDescription),
                Columns.Threads => Sort(p => p.ThreadCount),
                Columns.Gpu => Sort(p => p.GpuTimePercent),
                Columns.User => Sort(p => p.UserName),
                Columns.CommandLine => Sort(p => p.CmdLine),
                _ => filteredProcesses.OrderByDescending(p => p.CpuTimePercent)
            }).ToList();

            if (cmdLineFilters.NumProcs > -1) {
                sortedProcesses = sortedProcesses
                    .Take(cmdLineFilters.NumProcs)
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
