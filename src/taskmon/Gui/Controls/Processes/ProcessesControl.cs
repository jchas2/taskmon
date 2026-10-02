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

    protected override void OnGotFocus() => processControl.SetFocus();

    protected override void OnDraw()
    {
        processControl.Draw();
        processInfoControl.Draw();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        switch (keyInfo.Key) {
            case ConsoleKey.RightArrow when processControl.HasFocus && !processControl.IsSortSelectionActive:
                processInfoControl.SetFocus();
                handled = true;
                Draw();
                break;

            case ConsoleKey.LeftArrow when processInfoControl.HasFocus:
                processInfoControl.KeyPressed(keyInfo, ref handled);

                if (!handled) {
                    processControl.SetFocus();
                    handled = true;
                    processInfoControl.ResetToDetail();
                    processInfoControl.LoadProcess(processControl.SelectedProcessId);
                }

                Draw();
                break;

            default:
                Control targetControl = processInfoControl.HasFocus
                    ? processInfoControl
                    : processControl;
                
                targetControl.KeyPressed(keyInfo, ref handled);
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
        processControl.SelectedProcessIdChanged += OnSelectedProcessIdChanged;

        base.OnLoad();
    }

    private void OnSelectedProcessIdChanged(object? sender, int pid)
    {
        if (processInfoControl.HasFocus) {
            return;
        }

        processInfoControl.LoadProcess(pid);
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
        processControl.SelectedProcessIdChanged -= OnSelectedProcessIdChanged;
        base.OnUnload();
    }
}
