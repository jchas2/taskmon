using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Process;
using WorkerTask = System.Threading.Tasks.Task;
    
namespace Task.Monitor.Gui.Controls.Processes;

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
    private bool modulesLoaded;

    private CancellationTokenSource? cancellationTokenSource;

    private const int MenuViewWidth = 10;

    private const string MsgNotYetImplemented = "Not yet implemented on this OS";
    private const string MsgThreadsNotLoaded = "Threads could not be loaded";
    private const string MsgModulesNotLoaded = "Modules could not be loaded";
    private const string MsgLoading = "Loading, please wait...";

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
            EmptyListViewText = MsgThreadsNotLoaded,
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
            EmptyListViewText = MsgModulesNotLoaded,
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
            processInfoView,
            threadsView,
            modulesView,
            handlesView
        });
    }

    public bool AutoRefresh { get; set; } = true;

    private void MenuViewOnItemClicked(object? sender, ListViewItemEventArgs e)
    {
        var menuListViewItem = e.Item as MenuListViewItem;
        SetActiveControl(menuListViewItem!.AssociatedControl);
        menuListViewItem.LoadItems?.Invoke();

        Clear();
        Resize();
        Draw();
    }

    protected override void OnDraw()
    {
        ListView activeControl = tabControls.Single(ctrl => ctrl.Visible);
        menuView.Draw();
        activeControl.Draw();
    }

    // ProcessInfoControl itself draws no border - menuView is the actual bordered, focusable
    // panel that owns internal left/right routing to whichever tab is active - so a SetFocus()
    // call on this composite (e.g. from ProcessesControl's arrow-key nav) needs to be redirected
    // down to it for the focus-colour cue to reach anything visible.
    protected override void OnGotFocus() => menuView.SetFocus();

    public override bool HasFocus => GetFocusedControl?.HasFocus ?? false;

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        ListView activeControl = tabControls.Single(ctrl => ctrl.Visible);

        switch (keyInfo.Key) {
            // Only claimed when there is somewhere internal left/right to move: on the active
            // tab, left steps back to the menu; on the menu, right steps into the active tab.
            // Otherwise the key is left unhandled so ProcessesControl can move focus back to
            // processControl (left) or leaves right to do nothing further (there is no pane
            // beyond the active tab).
            case ConsoleKey.LeftArrow when GetFocusedControl == activeControl:
                menuView.SetFocus();
                handled = true;
                Draw();
                break;

            case ConsoleKey.RightArrow when GetFocusedControl == menuView:
                if (activeControl.Items.Count > 0) {
                    activeControl.SelectedIndex = 0;
                }

                activeControl.SetFocus();
                handled = true;
                Draw();
                break;

            default:
                GetFocusedControl?.KeyPressed(keyInfo, ref handled);
                break;
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;
        
        ListView[] listViews = [
            menuView, 
            processInfoView, 
            modulesView, 
            threadsView, 
            handlesView];

        foreach (var listView in listViews) {
            listView.BackgroundHighlightColour = appConfig.Theme.BackgroundHighlight;
            listView.ForegroundHighlightColour = appConfig.Theme.ForegroundHighlight;
            listView.BackgroundHighlightInactiveColour = appConfig.Theme.BackgroundHighlightInactive;
            listView.ForegroundHighlightInactiveColour = appConfig.Theme.ForegroundHighlightInactive;
            listView.BackgroundColour = appConfig.Theme.ListViewBackground;
            listView.ForegroundColour = appConfig.Theme.ListViewForeground;
            listView.BorderForegroundColour = appConfig.Theme.ListViewBorderForeground;
            listView.BorderBackgroundColour = appConfig.Theme.ListViewBorderBackground;
            listView.HeaderBackgroundColour = appConfig.Theme.HeaderBackground;
            listView.HeaderForegroundColour = appConfig.Theme.HeaderForeground;
        }

        menuView.Items.Add(
            new MenuListViewItem(
                processInfoView,
                "DETAIL",
                appConfig.Theme.ListViewBackground,
                appConfig.Theme.ListViewForeground));

        menuView.Items.Add(
            new MenuListViewItem(
                threadsView,
                "THREADS",
                appConfig.Theme.ListViewBackground,
                appConfig.Theme.ListViewForeground));

        menuView.Items.Add(
            new MenuListViewItem(
                modulesView, "MODULES",
                appConfig.Theme.ListViewBackground,
                appConfig.Theme.ListViewForeground) {
                LoadItems = TryUpdateListViewModuleItems
            });

        menuView.Items.Add(
            new MenuListViewItem(
                handlesView,
                "HANDLES",
                appConfig.Theme.ListViewBackground,
                appConfig.Theme.ListViewForeground));

        TryLoadProcessInfo();
        TryUpdateListViewThreadItems();
        SetActiveControl(processInfoView);

        menuView.SetFocus();
        menuView.ItemClicked += MenuViewOnItemClicked;
        menuView.SelectedIndex = 0;

        cancellationTokenSource = null;
        workerTask = null;

        if (AutoRefresh) {
            cancellationTokenSource = new CancellationTokenSource();
            workerTask = WorkerTask.Run(() => UpdateListViewThreadItemsLoop(cancellationTokenSource.Token));
            Trace.WriteLine($"ProcessInfoControl worker task {nameof(workerTask)} spawned with id {workerTask.Id}.");
        }
        
        base.OnLoad();
    }

    protected override void OnResize()
    {
        menuView.X = X;
        menuView.Y = Y;
        menuView.Height = Height;
        menuView.Width = MenuViewWidth;
        // The column width is the *inner* content width, not the outer control width - the two
        // border columns aren't part of it (see MenuControl.OnResize's identical Width - 2).
        // Setting it to the full outer width made DrawItem()'s columnWidth > viewPort.Bounds.Width
        // guard trip on every row, silently blanking all menu item text.
        menuView.ColumnHeaders[0].Width = MenuViewWidth - 2;

        tabControls.ForEach(ctrl => {
            ctrl.X = menuView.X + menuView.Width;
            ctrl.Y = menuView.Y;
            ctrl.Height = menuView.Height;
            ctrl.Width = Width - menuView.Width;
        });

        // The second column's width must leave room for the two border columns DrawItem()'s
        // viewport actually has to draw into (see CalculateViewPortBounds: Width - inset*2) -
        // using the raw outer Width here made the two columns sum to 2 more than the viewport,
        // which tripped DrawItem()'s columnWidth-vs-viewport guard and silently blanked the
        // second column's text on every row (labels rendered, values never did).
        processInfoView.ColumnHeaders[(int)InfoColumns.Key].Width = ColumnInfoKeyWidth;
        processInfoView.ColumnHeaders[(int)InfoColumns.Value].Width = processInfoView.Width - ColumnInfoKeyWidth - 2;

        modulesView.ColumnHeaders[(int)ModuleColumns.ModuleName].Width = ColumnModuleNameWidth;
        modulesView.ColumnHeaders[(int)ModuleColumns.FileName].Width = modulesView.Width - ColumnModuleNameWidth - 2;
        
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
        
        base.OnResize();
    }

    protected override void OnUnload()
    {
        try {
            cancellationTokenSource?.Cancel();
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

        modulesLoaded = false;
        
        menuView.ItemClicked -= MenuViewOnItemClicked;
        
        base.OnUnload();
    }

    public int SelectedProcessId { get; set; } = -1;

    // The sync entry point for a host (ProcessesControl) telling this control the highlighted
    // process elsewhere changed. Detail and threads always refresh - both are cheap and threads
    // already refresh every second regardless; modules stay lazy, only forced when MODULES
    // happens to be the tab currently on screen, matching the existing lazy-load-once philosophy.
    public void LoadProcess(int pid)
    {
        if (pid == SelectedProcessId) {
            return;
        }

        SelectedProcessId = pid;
        processInfoView.Items.Clear();
        modulesLoaded = false;
        modulesView.Items.Clear();

        TryLoadProcessInfo();
        TryUpdateListViewThreadItems();

        if (modulesView.Visible) {
            TryUpdateListViewModuleItems();
        }

        Draw();
    }

    // Called by the host (ProcessesControl) when focus leaves this control, so pid auto-binding
    // only ever refreshes the cheap DETAIL pane - never MODULES (which can shell out on macOS) or
    // THREADS (the 1s worker loop only runs while threadsView is visible). Both lines are needed:
    // the SelectedIndex setter only moves the highlight, it doesn't raise ItemClicked. No Draw()
    // here - the host redraws straight after.
    public void ResetToDetail()
    {
        SetActiveControl(processInfoView);
        menuView.SelectedIndex = 0;
    }

    // Test-only seams: which menu row is highlighted, and whether DETAIL is the tab on screen.
    internal int SelectedMenuIndexForTests => menuView.SelectedIndex;

    internal bool IsDetailActiveForTests => processInfoView.Visible;

    // Test-only seam: invokes the same click handler a real menu selection (click or arrow-key
    // move onto the row) would fire, without needing to simulate real key presses.
    internal void SelectMenuItemForTests(int index) =>
        MenuViewOnItemClicked(this, new ListViewItemEventArgs(menuView.Items[index]));

    // Test-only seam: the process name and pid shown centred in the top border of every tab view.
    internal string ProcessTitle => processInfoView.HeaderText;

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
                    appConfig.Theme.ListViewBackground,
                    appConfig.Theme.ListViewForeground));
                    
            processInfoView.Items.Add(
                new(["Description:", processInfo.FileDescription],
                    appConfig.Theme.ListViewBackground,
                    appConfig.Theme.ListViewForeground));
                    
            processInfoView.Items.Add(
                new(["Path:", processInfo.CmdLine],
                    appConfig.Theme.ListViewBackground,
                    appConfig.Theme.ListViewForeground));
            
            processInfoView.Items.Add(
                new(["User:", processInfo.UserName],
                    appConfig.Theme.ListViewBackground,
                    appConfig.Theme.ListViewForeground));
            
            processInfoView.Items.Add(
                new(["Version:", fvi.FileVersion ?? string.Empty],
                    appConfig.Theme.ListViewBackground,
                    appConfig.Theme.ListViewForeground));
            
            processInfoView.Items.Add(
                new(["Size:", finfo.Length.ToFormattedByteSize()],
                    appConfig.Theme.ListViewBackground,
                    appConfig.Theme.ListViewForeground));
            
            processInfoView.Items.Add(
                new(["Size on disk:", $"{finfo.Length} bytes"],
                    appConfig.Theme.ListViewBackground,
                    appConfig.Theme.ListViewForeground));
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"Error loading ProcessInfo for pid {SelectedProcessId}.");
            processInfoView.Items.Add(new(new[] { "Error:", ex.Message.ToRed() }));
        }
    }
    
    private void TryUpdateListViewModuleItems()
    {
        if (modulesLoaded) {
            return;
        }

        string prevEmptyText = modulesView.EmptyListViewText;
        
        try {
            Control.DrawingLockAcquire();
            modulesView.EmptyListViewText = MsgLoading;
            modulesView.Items.Clear();
            modulesView.Draw();

            List<ModuleInfo> modules = moduleService.GetModules(SelectedProcessId)
                .OrderBy(m => m.ModuleName)
                .ToList();

            if (modules.Count > 0) {
                foreach (var moduleInfo in modules) {
                    modulesView.Items.Add(new ModuleListViewItem(moduleInfo, appConfig));
                }
            }
            else {
                Trace.WriteLine($"GetModules returned 0 for pid {SelectedProcessId}.");
            }

            modulesLoaded = true;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"Error loading ModuleInfos for pid {SelectedProcessId}.");
        }
        finally {
            modulesView.EmptyListViewText = prevEmptyText;
            Control.DrawingLockRelease();
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
                Trace.WriteLine($"GetThreads returned 0 for pid {SelectedProcessId}.");
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
            ExceptionHelper.LogException(ex, $"Error loading ThreadInfos for pid {SelectedProcessId}.");
        }
        finally {
            Control.DrawingLockRelease();
        }
    }
    
    private void UpdateListViewThreadItemsLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested) {
            if (threadsView.Visible) {
                TryUpdateListViewThreadItems();
                Draw();
            }
            Thread.Sleep(1000);
        }
        
        Trace.WriteLine("ProcessInfoControl CancellationToken signalled.");
    }
}
