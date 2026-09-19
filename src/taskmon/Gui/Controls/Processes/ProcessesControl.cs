using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Process;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.Gui.Controls.Processes;

public sealed class ProcessesControl : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private SystemSnapshot? snapshot;
    private readonly ProcessControl processControl;
    private readonly ProcessInfoControl processInfoControl;

    private const int MinWidth = 20;
    private const int MinHeight = 4;

    public ProcessesControl(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;

        processControl = new ProcessControl(
            serviceController,
            terminal,
            appConfig) {
            TabStop = true,
            TabIndex = 1
        };

        // ProcessService/ModuleService/ThreadService are plain on-demand query classes (not
        // ServiceController-registered worker services), so they're constructed directly here
        // rather than resolved via serviceController.GetService<T>().
        processInfoControl = new ProcessInfoControl(
            new Task.Monitor.System.Process.ProcessService(),
            new ModuleService(),
            new ThreadService(),
            terminal,
            appConfig) {
            TabStop = true,
            TabIndex = 2
        };

        Controls.Add(processControl).Add(processInfoControl);
    }

    internal ProcessControl ProcessControl => processControl;

    internal ProcessInfoControl ProcessInfoControl => processInfoControl;

    // ProcessesControl itself draws no border - it delegates entirely to processControl, whose
    // own OnGotFocus redirects further down to whichever list is active - so a SetFocus() call
    // on this composite needs to be redirected there for the focus-colour cue to reach anything
    // visible.
    protected override void OnGotFocus() => processControl.SetFocus();

    private void UpdateProcessHeaderAndFooter()
    {
        ProcessMetrics? metrics = snapshot?.Processes?.Metrics;

        processControl.HeaderText =
            $"Processes    {metrics?.ProcessCount ?? 0} Total    {metrics?.ThreadCount ?? 0} Threads    {metrics?.RunningCount ?? 0} Running";
        processControl.FooterText =
            "Pg Up | Pg Down | ↓ Scroll Down | ↑ Scroll Up | Sort Asc: a | Sort Desc: d";
    }

    protected override void OnDraw()
    {
        UpdateProcessHeaderAndFooter();
        processControl.Draw();
        processInfoControl.Draw();
    }

    // Neither child's own Focused is a usable signal here: both ProcessControl.OnGotFocus and
    // ProcessInfoControl.OnGotFocus redirect focus one level further down as soon as they receive
    // it (to processView/sortView, and to menuView, respectively - see Screen.FocusInternal), so
    // Focused on the child itself flips back to false the instant that happens. HasFocus on each
    // is the proxy that survives that redirect.
    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        switch (keyInfo.Key) {
            case ConsoleKey.RightArrow when processControl.HasFocus:
                processInfoControl.SetFocus();
                handled = true;
                Draw();
                break;

            // processInfoControl gets first refusal on its own internal left/right nav (its
            // menu <-> active tab chain). Only step back out to processControl once it reports
            // there is nothing further left inside it.
            case ConsoleKey.LeftArrow when processInfoControl.HasFocus:
                processInfoControl.KeyPressed(keyInfo, ref handled);

                if (!handled) {
                    processControl.SetFocus();
                    handled = true;

                    // OnSelectedProcessIdChanged ignored every change while processInfoControl had
                    // focus (see its own comment), and ProcessControl's diff check only fires on an
                    // actual change - so if the pid drifted while we were ignoring it and has been
                    // steady since, nothing would otherwise ever tell processInfoControl to catch
                    // up. Forcing it here, the moment focus actually leaves, closes that gap.
                    processInfoControl.LoadProcess(processControl.SelectedProcessId);
                }

                Draw();
                break;

            default:
                (processInfoControl.HasFocus ? processInfoControl : (Control)processControl)
                    .KeyPressed(keyInfo, ref handled);
                break;
        }
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;

        foreach (Control ctrl in Controls) {
            ctrl.BackgroundColour = appConfig.Theme.Background;
            ctrl.ForegroundColour = appConfig.Theme.Foreground;
        }

        processControl.NumberOfProcesses = -1;
        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
        processControl.SelectedProcessIdChanged += OnSelectedProcessIdChanged;

        base.OnLoad();
    }

    // While processInfoControl has focus, the user is actively scrolling one of its own lists
    // (DETAIL/THREADS/MODULES/HANDLES) - reloading out from under them whenever the pid grid's
    // selection shifts (a background sort re-publish moving a different process under the same
    // row, not necessarily anything the user did) would yank them to a different pid mid-scroll.
    // Syncing resumes, with an explicit catch-up, the moment focus actually leaves - see the
    // LeftArrow case in OnKeyPressed.
    private void OnSelectedProcessIdChanged(object? sender, int pid)
    {
        if (processInfoControl.HasFocus) {
            return;
        }

        processInfoControl.LoadProcess(pid);
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        snapshot = e.Snapshot;

        try {
            Control.DrawingLockAcquire();

            // processControl redraws itself via its own SystemSnapshotUpdated subscription (which
            // registers after this one - see OnLoad), which is also what refreshes the data it
            // draws. Calling processControl.Draw() here too, before that subscription's handler
            // has run, would repaint last tick's still-unchanged data and produce a spurious
            // "reverted to plain" flash sandwiched between this tick's real change and the correct
            // redraw moments later. Only the header/footer text needs to be current by then.
            UpdateProcessHeaderAndFooter();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    protected override void OnResize()
    {
        if (Width < MinWidth || Height < MinHeight) {
            return;
        }

        int processHeight = (int)(Height * 0.6);

        processControl.X = X;
        processControl.Y = Y;
        processControl.Width = Width;
        processControl.Height = processHeight;

        processInfoControl.X = X;
        processInfoControl.Y = Y + processHeight;
        processInfoControl.Width = Width;
        processInfoControl.Height = Math.Max(1, Height - processHeight);

        base.OnResize();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        processControl.SelectedProcessIdChanged -= OnSelectedProcessIdChanged;
        base.OnUnload();
    }
}
