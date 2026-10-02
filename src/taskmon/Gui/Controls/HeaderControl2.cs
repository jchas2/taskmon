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

    private readonly string machineName = Environment.MachineName.ToUpper();
    private readonly string osVersion = SystemInfo.GetOsVersion();

    private CpuInfo? cpuInfo;
    private ProcessInfo? processInfo;
    private string privateIPv4Address = string.Empty;

#if __APPLE__
    // Only the Apple header reports gpu cores, so the field is only kept where it is drawn.
    private GpuInfo? gpuInfo;
#endif

    private static readonly ProcessMetrics EmptyProcessMetrics = new();
    private readonly AnsiScreenBuffer frame = new();

    public const int HeaderRows = 5;

    public HeaderControl2(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;
    }

    protected override void OnDraw() => OnDrawInternal();

    private void OnDrawInternal()
    {
        const string MachineLabel = "Machine: ";
        const string OSLabel = " OS: ";
        const string IpLabel = " Ip: ";
        const string CpuLabel = "Cpu: ";
        const string TasksLabel = " Tasks: ";
        const string ThreadsLabel = " Threads: ";
        const string RunningLabel = " running";

        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;

        Terminal.SetCursorPosition(X, Y);
        Terminal.BackgroundColor = appConfig.Theme.MenubarBackground;
        Terminal.ForegroundColor = appConfig.Theme.MenubarForeground;

        string menubar = "TASK MONITOR";
        int offsetX = Terminal.WindowWidth / 2 - menubar.Length / 2;

        Terminal.WriteEmptyLineTo(offsetX);
        Terminal.Write(menubar);
        Terminal.WriteEmptyLineTo(Width - offsetX - menubar.Length);

        Terminal.BackgroundColor = BackgroundColour;
        Terminal.ForegroundColor = ForegroundColour;

        int innerWidth = Width - 2;

        if (innerWidth < 1) {
            return;
        }

        Color lbColor = appConfig.Theme.Foreground;
        Color fgColour = appConfig.Theme.RangeLowBackground;
        Color bgColour = appConfig.Theme.Background;
        Color borderColour = appConfig.Theme.ControlBorder;

        frame.Clear();

        frame.MoveTo(X, Y + 1);
        frame.SetColour(borderColour, bgColour);
        frame.Append('╭');
        frame.Append('─', innerWidth);
        frame.Append('╮');

        string ipAddress = privateIPv4Address;

        BeginRow(Y + 2, borderColour, bgColour);
        int remaining = innerWidth;

        AppendSegment(MachineLabel, lbColor, bgColour, ref remaining);
        AppendSegment(machineName, fgColour, bgColour, ref remaining);
        AppendSegment(OSLabel, lbColor, bgColour, ref remaining);
        AppendSegment(osVersion, fgColour, bgColour, ref remaining);
        AppendSegment(IpLabel, lbColor, bgColour, ref remaining);
        AppendSegment(ipAddress, fgColour, bgColour, ref remaining);

        string themeText = $"{appConfig.Theme.Name} ";

        if (themeText.Length <= remaining) {
            AppendPadding(remaining - themeText.Length, bgColour);
            remaining = themeText.Length;
            AppendSegment(themeText, fgColour, bgColour, ref remaining);
        }

        EndRow(remaining, borderColour, bgColour);
        
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

        bool irixMode = processInfo?.Specs.IrixMode ?? appConfig.UseIrixReporting;

        if (irixMode) {
            cpuInfoText += " Irix Mode";
        }
        else {
            cpuInfoText += " Solaris Mode";
        }

        ProcessMetrics processMetrics = processInfo?.Metrics ?? EmptyProcessMetrics;

        string processCount = processMetrics.ProcessCount.ToString();
        string threadCount = processMetrics.ThreadCount.ToString();
        string runningCount = processMetrics.RunningCount.ToString();

        BeginRow(Y + 3, borderColour, bgColour);
        remaining = innerWidth;

        AppendSegment(CpuLabel, lbColor, bgColour, ref remaining);
        AppendSegment(cpuInfoText, fgColour, bgColour, ref remaining);
        AppendSegment(TasksLabel, lbColor, bgColour, ref remaining);
        AppendSegment(processCount, fgColour, bgColour, ref remaining);
        AppendSegment(ThreadsLabel, lbColor, bgColour, ref remaining);
        AppendSegment(threadCount + ", ", fgColour, bgColour, ref remaining);
        AppendSegment(runningCount, fgColour, bgColour, ref remaining);
        AppendSegment(RunningLabel, lbColor, bgColour, ref remaining);

        EndRow(remaining, borderColour, bgColour);

        frame.MoveTo(X, Y + 4);
        frame.SetColour(borderColour, bgColour);
        frame.Append('╰');
        frame.Append('─', innerWidth);
        frame.Append('╯');

        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }

    private void BeginRow(int y, Color borderColour, Color bgColour)
    {
        frame.MoveTo(X, y);
        frame.SetColour(borderColour, bgColour);
        frame.Append('│');
    }

    private void EndRow(int remaining, Color borderColour, Color bgColour)
    {
        AppendPadding(remaining, bgColour);
        frame.SetColour(borderColour, bgColour);
        frame.Append('│');
    }

    private void AppendSegment(string text, Color fgColour, Color bgColour, ref int remaining)
    {
        int length = Math.Min(text.Length, remaining);

        if (length <= 0) {
            return;
        }

        frame.SetColour(fgColour, bgColour);
        frame.Append(text.AsSpan(0, length));
        remaining -= length;
    }

    private void AppendPadding(int count, Color bgColour)
    {
        if (count <= 0) {
            return;
        }

        frame.SetColour(ForegroundColour, bgColour);
        frame.Append(' ', count);
    }

    protected override void OnLoad()
    {
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;

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
