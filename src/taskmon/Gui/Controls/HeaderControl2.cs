using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.Gui.Controls;

public sealed class HeaderControl2 : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;

    // The host's own identity is fixed for the life of the process, so it is resolved once here
    // rather than re-read out of a snapshot that would carry the same answer every cycle.
    private readonly string machineName = Environment.MachineName.ToUpper();
    private readonly string osVersion = SystemInfo.GetOsVersion();

    // Each service publishes independently, so a snapshot can carry one of these and not another.
    // The latest of each is retained and the header draws whatever it has.
    private CpuInfo? cpuInfo;
    private ProcessInfo? processInfo;
    private string privateIPv4Address = string.Empty;

#if __APPLE__
    // Only the Apple header reports gpu cores, so the field is only kept where it is drawn.
    private GpuInfo? gpuInfo;
#endif

    // A stand-in for the first draw, before the process service has published anything.
    private static readonly ProcessMetrics EmptyProcessMetrics = new();

    private const int MinHeaderRows = 3;

    public HeaderControl2(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;
    }

    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();
            OnDrawInternal();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    private void OnDrawInternal()
    {
        const string MachineLabel = "Machine: ";
        const string OSLabel = " OS: ";
        const string IpLabel = " Ip: ";
        const string CpuLabel = "Cpu: ";
        const string TasksLabel = " Tasks: ";
        const string ThreadsLabel = " Threads: ";
        const string RunningLabel = " running";

        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;

        Terminal.SetCursorPosition(X, Y);
        Terminal.BackgroundColor = appConfig.DefaultTheme.MenubarBackground;
        Terminal.ForegroundColor = appConfig.DefaultTheme.MenubarForeground;

        string menubar = "TASK MONITOR";
        int offsetX = Terminal.WindowWidth / 2 - menubar.Length / 2;

        Terminal.WriteEmptyLineTo(offsetX);
        Terminal.Write(menubar);
        Terminal.WriteEmptyLineTo(Width - offsetX - menubar.Length);

        Terminal.BackgroundColor = BackgroundColour;
        Terminal.ForegroundColor = ForegroundColour;

        Color lbColor = appConfig.DefaultTheme.Foreground;
        Color fgColour = appConfig.DefaultTheme.RangeLowBackground;
        Color bgColour = appConfig.DefaultTheme.Background;

        string ipAddress = privateIPv4Address;

        Terminal.Write(MachineLabel.ToColour(lbColor, bgColour));
        Terminal.Write(machineName.ToColour(fgColour, bgColour));
        Terminal.Write(OSLabel.ToColour(lbColor, bgColour));
        Terminal.Write(osVersion.ToColour(fgColour, bgColour));
        Terminal.Write(IpLabel.ToColour(lbColor, bgColour));
        Terminal.Write(ipAddress.ToColour(fgColour, bgColour));

        int nchars =
            MachineLabel.Length + machineName.Length +
            OSLabel.Length + osVersion.Length +
            IpLabel.Length + ipAddress.Length;

        int themeLen = appConfig.DefaultTheme.Name.Length + 1;

        Terminal.BackgroundColor = bgColour;
        Terminal.WriteEmptyLineTo(Width - nchars - themeLen);
        Terminal.Write($"{appConfig.DefaultTheme.Name.ToColour(fgColour, bgColour)} ");

        // Nothing has published yet on the first draw. The header still paints its chrome and its
        // labels, with the figures left at zero, rather than leaving the top rows unwritten.
        CpuSpecs cpuSpecs = cpuInfo?.Specs ?? default;

        string coreBreakdown = $"{cpuSpecs.CpuCores} Cores";
#if __APPLE__
        if (cpuSpecs.CpuPerformanceCores > 0) {
            coreBreakdown += $" · {cpuSpecs.CpuPerformanceCores}P";
        }

        if (cpuSpecs.CpuEfficiencyCores > 0) {
            coreBreakdown += $" · {cpuSpecs.CpuEfficiencyCores}E";
        }

        if (cpuSpecs.CpuSuperCores > 0) {
            coreBreakdown += $" · {cpuSpecs.CpuSuperCores}S";
        }

        int gpuCores = gpuInfo?.Specs.GpuCores ?? 0;

        if (gpuCores > 0) {
            coreBreakdown += $" · {gpuCores} Gpu";
        }
#endif
        string cpuName = cpuSpecs.CpuName ?? string.Empty;

        if (cpuSpecs.CpuFrequency > 0) {
            cpuName += $" @ {cpuSpecs.ToCpuFrequencyGhz()}";
        }

        string cpuInfoText = $"{cpuName} ({coreBreakdown})";

        // Irix mode is the process service's, not the config's: it is the setting the percentages
        // in the list below were actually calculated with.
        bool irixMode = processInfo?.Specs.IrixMode ?? appConfig.UseIrixReporting;

        if (irixMode) {
            cpuInfoText += " Irix Mode";
        }
        else {
            cpuInfoText += " Solaris Mode";
        }

        Terminal.Write(CpuLabel.ToColour(lbColor, bgColour));
        Terminal.Write(cpuInfoText.ToColour(fgColour, bgColour));
        nchars = CpuLabel.Length + cpuInfoText.Length;

        ProcessMetrics processMetrics = processInfo?.Metrics ?? EmptyProcessMetrics;

        string processCount = processMetrics.ProcessCount.ToString();
        string threadCount = processMetrics.ThreadCount.ToString();
        string runningCount = processMetrics.RunningCount.ToString();

        Terminal.Write(TasksLabel.ToColour(lbColor, bgColour));
        Terminal.Write(processCount.ToColour(fgColour, bgColour));
        Terminal.Write(ThreadsLabel.ToColour(lbColor, bgColour));
        Terminal.Write((threadCount + ", ").ToColour(fgColour, bgColour));
        Terminal.Write(runningCount.ToColour(fgColour, bgColour));
        Terminal.Write(RunningLabel.ToColour(lbColor, bgColour));

        nchars += TasksLabel.Length + processCount.Length +
                  ThreadsLabel.Length + threadCount.Length + 2 +
                  runningCount.Length + RunningLabel.Length;

        Terminal.BackgroundColor = bgColour;
        Terminal.WriteEmptyLineTo(Width - nchars);
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.DefaultTheme.Background;
        ForegroundColour = appConfig.DefaultTheme.Foreground;

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        if (e.Snapshot.Cpu != null) {
            cpuInfo = e.Snapshot.Cpu;
        }

#if __APPLE__
        if (e.Snapshot.Gpu != null) {
            gpuInfo = e.Snapshot.Gpu;
        }
#endif

        if (e.Snapshot.Processes != null) {
            processInfo = e.Snapshot.Processes;
        }

        if (e.Snapshot.Network != null) {
            // Resolved on arrival rather than in the draw: an adapter list walk per repaint would
            // be repeated work for an answer that only changes when an adapter does.
            privateIPv4Address = e.Snapshot.Network.Specs.ToPreferredIPv4Address();
        }

        Draw();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        base.OnUnload();
    }
}
