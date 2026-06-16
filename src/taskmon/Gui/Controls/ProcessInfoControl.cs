using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Process;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Process;
using WorkerTask = System.Threading.Tasks.Task;
    
namespace Task.Monitor.Gui.Controls;

public partial class ProcessInfoControl : Control
{
    private readonly IProcessService processService;
    private readonly IModuleService moduleService;
    private readonly IThreadService threadService;
    private readonly AppConfig appConfig;
    private readonly ListView processInfoView;
    private readonly ListView menuView;
    private readonly ListView modulesView;
    private readonly ListView threadsView;
    private readonly ListView handlesView;
    private readonly List<ListView> tabControls = [];
    private WorkerTask? workerTask;
    
    private CancellationTokenSource? cancellationTokenSource;

    private const int ControlGutter = 1;
    private const int ProcessInfoViewHeight = 8;
    private const int MenuViewWidth = 10;

    private const string MsgNotYetImplemented = "Not yet implemented on this OS";

    public ProcessInfoControl(
        IProcessService processService,
        IModuleService moduleService,
        IThreadService threadService,
        ISystemTerminal terminal, 
        AppConfig appConfig) 
        : base(terminal)
    {
        this.processService = processService;
        this.moduleService = moduleService;
        this.threadService = threadService;
        this.appConfig = appConfig;
        
        menuView = new ListView(terminal) {
            TabStop = true,
            TabIndex = 1,
            Visible = true
        };

        menuView.ColumnHeaders.Add(new ListViewColumnHeader("SELECT"));
        
        processInfoView = new ListView(terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowColumnHeaders = true,
            TabStop = true,
            TabIndex = 2,
            Visible = true,
        };

        processInfoView.ColumnHeaders
            .Add(new ListViewColumnHeader("Pid:"))
            .Add(new ListViewColumnHeader(""));

        threadsView = new ListView(terminal) {
            TabStop = true,
            TabIndex = 3,
            Visible = true
        };

        threadsView.ColumnHeaders
            .Add(new ListViewColumnHeader("THREAD ID"))
            .Add(new ListViewColumnHeader("STATE"))
            .Add(new ListViewColumnHeader("REASON"))
            .Add(new ListViewColumnHeader("PRI"))
            .Add(new ListViewColumnHeader("START ADDRESS"))
            .Add(new ListViewColumnHeader("KERNEL TIME"))
            .Add(new ListViewColumnHeader("USER TIME"))
            .Add(new ListViewColumnHeader("TOTAL TIME"));
        
        modulesView = new ListView(terminal) {
            EmptyListViewText = MsgNotYetImplemented,
            TabStop = true,
            TabIndex = 4,
            Visible = false
        };

        modulesView.ColumnHeaders
            .Add(new ListViewColumnHeader("MODULE"))
            .Add(new ListViewColumnHeader("PATH"));

        handlesView = new ListView(terminal) {
            EmptyListViewText = MsgNotYetImplemented,
            TabStop = true,
            TabIndex = 5,
            Visible = false
        };

        handlesView.ColumnHeaders
            .Add(new ListViewColumnHeader("ID"))
            .Add(new ListViewColumnHeader("NAME"));

        Controls
            .Add(processInfoView)
            .Add(menuView)
            .Add(modulesView)
            .Add(threadsView)
            .Add(handlesView);
        
        tabControls.AddRange(new [] {
            modulesView, 
            threadsView, 
            handlesView
        });
    }

    public bool AutoRefresh { get; set; } = true;

    private void MenuViewOnItemClicked(object? sender, ListViewItemEventArgs e)
    {
        var menuListViewItem = e.Item as MenuListViewItem;
        SetActiveControl(menuListViewItem!.AssociatedControl);
        
        Clear();
        Resize();
        Draw();
    }

    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();
            ListView activeControl = tabControls.Single(ctrl => ctrl.Visible);
            processInfoView.Draw();
            menuView.Draw();
            activeControl.Draw();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        try {
            Control.DrawingLockAcquire();
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
                    if (activeControl.Items.Count > 0) {
                        activeControl.SelectedIndex = 0;
                    }

                    activeControl.SetFocus();
                    Draw();
                    break;
                
                case ConsoleKey.UpArrow:
                case ConsoleKey.DownArrow:
                case ConsoleKey.PageUp:
                case ConsoleKey.PageDown:
                    focusedControl?.KeyPressed(keyInfo, ref handled);
                    break;
            }
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
        
        ListView[] listViews = [
            menuView, 
            processInfoView, 
            modulesView, 
            threadsView, 
            handlesView];

        foreach (var listView in listViews) {
            listView.BackgroundHighlightColour = appConfig.DefaultTheme.BackgroundHighlight;
            listView.ForegroundHighlightColour = appConfig.DefaultTheme.ForegroundHighlight;
            listView.BackgroundColour = appConfig.DefaultTheme.Background;
            listView.ForegroundColour = appConfig.DefaultTheme.Foreground;
            listView.HeaderBackgroundColour = appConfig.DefaultTheme.HeaderBackground;
            listView.HeaderForegroundColour = appConfig.DefaultTheme.HeaderForeground;
        }

        menuView.Items.Add(
            new MenuListViewItem(
                threadsView, 
                "THREADS",
                appConfig.DefaultTheme.Background,
                appConfig.DefaultTheme.Foreground));
        
        menuView.Items.Add(
            new MenuListViewItem(
                modulesView, "MODULES",
                appConfig.DefaultTheme.Background,
                appConfig.DefaultTheme.Foreground));
        
        menuView.Items.Add(
            new MenuListViewItem(
                handlesView, 
                "HANDLES",
                appConfig.DefaultTheme.Background,
                appConfig.DefaultTheme.Foreground));

        TryLoadProcessInfo();
        TryUpdateListViewThreadItems();
        TryUpdateListViewModuleItems();
        SetActiveControl(threadsView);

        menuView.SetFocus();
        menuView.ItemClicked += MenuViewOnItemClicked;
        
        cancellationTokenSource = new CancellationTokenSource();

        workerTask = AutoRefresh
            ? WorkerTask.Run(() => UpdateListViewItemsLoop(cancellationTokenSource.Token))
            : null;
    }

    protected override void OnResize()
    {
        Clear();
        
        processInfoView.X = X;
        processInfoView.Y = Y;
        processInfoView.Width = Width;
        processInfoView.Height = ProcessInfoViewHeight;
        processInfoView.ColumnHeaders[(int)InfoColumns.Key].Width = ColumnInfoKeyWidth;
        processInfoView.ColumnHeaders[(int)InfoColumns.Value].Width = Width - ColumnInfoKeyWidth;
        processInfoView.Resize();
        
        menuView.X = X;
        menuView.Y = processInfoView.Y + processInfoView.Height + ControlGutter;
        menuView.Height = Height - (ProcessInfoViewHeight + 1 + ControlGutter);
        menuView.Width = MenuViewWidth;
        menuView.ColumnHeaders[0].Width = MenuViewWidth;
        menuView.Resize();

        tabControls.ForEach(ctrl => {
            ctrl.X = menuView.X + menuView.Width + ControlGutter;
            ctrl.Y = menuView.Y;
            ctrl.Height = menuView.Height;
            ctrl.Width = Width - (menuView.Width + ControlGutter); 
        });
        
        modulesView.ColumnHeaders[(int)ModuleColumns.ModuleName].Width = ColumnModuleNameWidth;
        modulesView.ColumnHeaders[(int)ModuleColumns.FileName].Width = modulesView.Width - ColumnModuleNameWidth;
        modulesView.Resize();
        
        threadsView.ColumnHeaders[(int)ThreadColumns.Id].Width = ColumnThreadIdWidth;
        threadsView.ColumnHeaders[(int)ThreadColumns.State].Width = ColumnThreadStateWidth;
        threadsView.ColumnHeaders[(int)ThreadColumns.Reason].Width = ColumnThreadReasonWidth;
        threadsView.ColumnHeaders[(int)ThreadColumns.Priority].Width = ColumnThreadPriorityWidth;
        threadsView.ColumnHeaders[(int)ThreadColumns.Priority].RightAligned = true;
        threadsView.ColumnHeaders[(int)ThreadColumns.StartAddress].Width = ColumnThreadStartAddressWidth;
        threadsView.ColumnHeaders[(int)ThreadColumns.CpuKernelTime].Width = ColumnThreadCpuKernelTimeWidth;
        threadsView.ColumnHeaders[(int)ThreadColumns.CpuKernelTime].RightAligned = true;
        threadsView.ColumnHeaders[(int)ThreadColumns.CpuUserTime].Width = ColumnThreadCpuUserTimeWidth;
        threadsView.ColumnHeaders[(int)ThreadColumns.CpuUserTime].RightAligned = true;
        threadsView.ColumnHeaders[(int)ThreadColumns.CpuTotalTime].Width = ColumnThreadCpuTotalTimeWidth;
        threadsView.ColumnHeaders[(int)ThreadColumns.CpuTotalTime].RightAligned = true;
        
        threadsView.Resize();
        
        handlesView.Resize();
    }

    protected override void OnUnload()
    {
        base.OnUnload();
        
        cancellationTokenSource?.Cancel();

        try {
            workerTask?.Wait();
        }
        catch (AggregateException aggEx) {
            ExceptionHelper.HandleWaitAllException(aggEx);
        }
        
        processInfoView.Items.Clear();
        menuView.Items.Clear();
        modulesView.Items.Clear();
        threadsView.Items.Clear();
        handlesView.Items.Clear();
        
        menuView.ItemClicked -= MenuViewOnItemClicked;
    }

    public int SelectedProcessId { get; set; } = -1;

    private void SetActiveControl(Control activeControl)
    {
        tabControls.ForEach(ctrl => ctrl.Visible = false);
        activeControl.Visible = true;
    }

    private void TryLoadProcessInfo()
    {
        try {
            ProcessInfo? processInfo = processService.GetProcessById(SelectedProcessId);
            if (processInfo == null) {
                processInfoView.Items.Clear();
                return;
            }

            FileInfo finfo = new(processInfo.FileName);
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(finfo.FullName);

            processInfoView.ColumnHeaders[0].Text = "Pid:";
            processInfoView.ColumnHeaders[1].Text = SelectedProcessId.ToString();
            
            processInfoView.Items.Add(
                new(["File:", processInfo.ModuleName],
                    appConfig.DefaultTheme.Background,
                    appConfig.DefaultTheme.Foreground));
                    
            processInfoView.Items.Add(
                new(["Description:", processInfo.FileDescription],
                    appConfig.DefaultTheme.Background,
                    appConfig.DefaultTheme.Foreground));
                    
            processInfoView.Items.Add(
                new(["Path:", processInfo.CmdLine],
                    appConfig.DefaultTheme.Background,
                    appConfig.DefaultTheme.Foreground));
            
            processInfoView.Items.Add(
                new(["User:", processInfo.UserName],
                    appConfig.DefaultTheme.Background,
                    appConfig.DefaultTheme.Foreground));
            
            processInfoView.Items.Add(
                new(["Version:", fvi.FileVersion ?? string.Empty],
                    appConfig.DefaultTheme.Background,
                    appConfig.DefaultTheme.Foreground));
            
            processInfoView.Items.Add(
                new(["Size:", finfo.Length.ToFormattedByteSize()],
                    appConfig.DefaultTheme.Background,
                    appConfig.DefaultTheme.Foreground));
            
            processInfoView.Items.Add(
                new(["Size on disk:", $"{finfo.Length} bytes"],
                    appConfig.DefaultTheme.Background,
                    appConfig.DefaultTheme.Foreground));
        }
        catch (Exception ex) {
            ExceptionHelper.HandleException(ex);
            processInfoView.Items.Add(new(new[] { "Error:", ex.Message.ToRed() }));
        }
    }
    
    private void TryUpdateListViewModuleItems()
    {
        try {
            List<ModuleInfo> modules = moduleService.GetModules(SelectedProcessId)
                .OrderBy(m => m.ModuleName)
                .ToList();

            foreach (var moduleInfo in modules) {
                modulesView.Items.Add(new ModuleListViewItem(moduleInfo, appConfig));
            }
        }
        catch (Exception ex) {
            ExceptionHelper.HandleException(ex);
        }
    }
    
    private void TryUpdateListViewThreadItems()
    {
        try {
            Control.DrawingLockAcquire();

            List<ThreadInfo> threads = threadService.GetThreads(SelectedProcessId)
                .OrderByDescending(t => t.CpuTotalTime.Ticks)
                .ToList();

            if (threads.Count == 0) {
                threadsView.Items.Clear();
                return;
            }

            int selectedIndex = threadsView.SelectedIndex;

            HashSet<int> sortedThreadIds = new(threads.Count);
            
            for (int i = 0; i < threads.Count; i++) {
                sortedThreadIds.Add(threads[i].ThreadId);
            }
            
            for (int i = threadsView.Items.Count - 1; i >= 0; i--) {
                var item = (ThreadListViewItem)threadsView.Items[i];

                if (!sortedThreadIds.Contains(item.ThreadId)) {
                    threadsView.Items.RemoveAt(i);
                }
            }

            var threadLookup = threadsView.Items.Cast<ThreadListViewItem>().ToDictionary(t => t.ThreadId);

            for (int i = 0; i < threads.Count; i++) {
                if (threadLookup.TryGetValue(threads[i].ThreadId, out var foundItem)) {
                    foundItem.UpdateItem(threads[i]);
                    int insertAt = Math.Min(i, threadsView.Items.Count - 1);
                    threadsView.Items.Remove(foundItem);
                    threadsView.Items.InsertAt(insertAt, foundItem);
                }
                else {
                    ThreadListViewItem item = new(threads[i], appConfig);
                    threadsView.Items.InsertAt(i, item);
                }
            }

            if (threadsView.Items.Count > 0) {
                threadsView.SelectedIndex = selectedIndex >= 0 && selectedIndex < threadsView.Items.Count
                    ? selectedIndex
                    : 0;
            }
        }
        catch (Exception ex) {
            ExceptionHelper.HandleException(ex);
        }
        finally {
            Control.DrawingLockRelease();
        }
    }
    
    private void UpdateListViewItemsLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested) {
            if (threadsView.Visible) {
                TryUpdateListViewThreadItems();
                Draw();
            }
            Thread.Sleep(1500);
        }
    }
}
