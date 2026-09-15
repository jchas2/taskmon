using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.Gui.Controls.Processes;

public sealed class ProcessesControl : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private SystemSnapshot? snapshot;
    private readonly ProcessControl processControl;

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

        Controls.Add(processControl);
    }

    internal ProcessControl ProcessControl => processControl;

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
        try {
            Control.DrawingLockAcquire();

            UpdateProcessHeaderAndFooter();
            processControl.Draw();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        processControl.KeyPressed(keyInfo, ref handled);
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;

        foreach (Control ctrl in Controls) {
            ctrl.BackgroundColour = appConfig.DefaultTheme.Background;
            ctrl.ForegroundColour = appConfig.DefaultTheme.Foreground;
        }

        processControl.NumberOfProcesses = -1;
        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
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

        processControl.X = X;
        processControl.Y = Y;
        processControl.Width = Width - 2;
        processControl.Height = Height;

        base.OnResize();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        base.OnUnload();
    }
}
