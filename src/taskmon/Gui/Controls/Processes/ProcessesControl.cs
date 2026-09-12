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

    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();

            ProcessMetrics? metrics = snapshot?.Processes?.Metrics;

            processControl.HeaderText =
                $"Processes    {metrics?.ProcessCount ?? 0} Total    {metrics?.ThreadCount ?? 0} Threads    {metrics?.RunningCount ?? 0} Running";
            processControl.FooterText =
                "Pg Up | Pg Down | ↓ Scroll Down | ↑ Scroll Up | Sort Asc: a | Sort Desc: d";

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
        Draw();
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
