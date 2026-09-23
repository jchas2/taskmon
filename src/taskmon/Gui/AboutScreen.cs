using System.Drawing;
using System.Linq;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Extensions;
using Task.Monitor.Gui.Controls;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.Gui;

public sealed class AboutScreen : Screen
{
#if __APPLE__
    private static readonly string[] Hex =
    {
        "3CC846", "3CC846", "3CC846", "3CC846", "3CC846", // Green.
        "E63C32", "E63C32",                               // Red.
        "F08C1E", "F08C1E",                               // Orange.
        "EBD228", "EBD228",                               // Yellow.
        "3CC846", "3CC846",                               // Green.
        "28C8D2", "28C8D2",                               // Cyan.
        "326EE6", "326EE6",                               // Blue.
        "BE46C8", "BE46C8", "BE46C8",                     // Magenta.
    };
#endif
#if __WIN32__
    private static readonly string[] Hex =
    {
        "00A4EF", "00A4EF", "00A4EF", "00A4EF", // Blue.
        "7FBA00", "7FBA00", "7FBA00", "7FBA00", // Green.
        "F25022", "F25022", "F25022", "F25022", // Red.
        "FFB900", "FFB900", "FFB900", "FFB900", // Yellow.
    };
#endif

    // The glyph text itself is shared with SystemLogoControl (see LogoArt) - only the colour
    // strategy differs between the two: this screen rotates colors[] every draw for a marquee
    // effect, SystemLogoControl keeps its colours fixed and pulses bold instead.
    private static readonly (string text, string hex)[] Art =
        LogoArt.Lines.Zip(Hex, (text, hex) => (text, hex)).ToArray();

    private string[] colors = new string[Art.Length];
    private readonly RunContext runContext;

    // The most recent snapshot, kept whole rather than unpacked into fields: every row on this
    // screen is read once per draw, so there is nothing to gain from copying them out first.
    private SystemSnapshot? snapshot;

    // Neither is published by a service, and neither changes while the process is running.
    private readonly string machineName = Environment.MachineName.ToUpper();
    private readonly string osVersion = SystemInfo.GetOsVersion();

    private readonly ListView statsView;
    private string menubar;
    private string version;

    public AboutScreen(RunContext runContext) : base(runContext.Terminal)
    {
        this.runContext = runContext;
        menubar = "ABOUT TASK MONITOR";
        version = $"Version {AssemblyVersionInfo.GetVersion()}";

        statsView = new ListView(runContext.Terminal) {
            EnableScroll = false,
            EnableRowSelect = false,
            ShowColumnHeaders = false,
            TabStop = true,
            TabIndex = 0,
            Visible = true
        };

        statsView.ColumnHeaders
            .Add(new ListViewColumnHeader(""))
            .Add(new ListViewColumnHeader(""));
        
        Controls.Add(statsView);
    }

    private void DrawInternal()
    {
        AnsiScreenBuffer frame = new();
        frame.MoveTo(X, Y);
        
        frame.SetColour(
            runContext.AppConfig.Theme.MenubarForeground, 
            runContext.AppConfig.Theme.MenubarBackground);
        
        int offsetX = Terminal.WindowWidth / 2 - menubar.Length / 2;

        frame.Append(' ', offsetX);
        frame.Append(menubar);
        frame.Append(' ', Width - offsetX - menubar.Length);
        
        frame.SetColour(
            runContext.AppConfig.Theme.Foreground, 
            runContext.AppConfig.Theme.Background);
        
        offsetX = Terminal.WindowWidth / 2 - version.Length / 2;
        
        frame.Append(' ', offsetX);
        frame.Append(version);
        frame.Append(' ', Width - offsetX - version.Length);
        
        int offsetY = 2;

        for (int i = 0; i < offsetY; i++) {
            frame.Append(' ', Width);
        }

        int logoX = 4;

        for (int i = 0; i < Art.Length; i++) {
            var (text, _) = Art[i];
            Color colour = ConsolePalette.FromHex(colors[i], ConsolePalette.Black);
            string colourCode = ConsolePalette.ForegroundSgr(colour);
            frame.Append(' ', logoX);
            frame.Append(colourCode + text + "\u001b[K");
            frame.Append(Environment.NewLine);
        }

        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
        
        string last = colors[^1];

        for (int i = colors.Length - 1; i > 0; i--) {
            colors[i] = colors[i - 1];
        }

        colors[0] = last;

        if (snapshot == null) {
            return;
        }

        // Neither of these comes from a service and neither changes while the process runs.
        statsView.Items[0].SubItems[1].Text = machineName;
        statsView.Items[1].SubItems[1].Text = osVersion;

        // Each block is guarded on its own. The services publish independently, so a snapshot can
        // carry a cpu reading and not yet a gpu one; a row whose service has not reported keeps
        // the value it was last given rather than being blanked back to zero.
        UpdateCpuRows(snapshot);
        UpdateMemoryRows(snapshot);
        UpdateGpuRows(snapshot);
        UpdateNetworkRows(snapshot);
        UpdateDiskRows(snapshot);
        UpdateTopProcessRows(snapshot);

        statsView.Draw();

        KeyBindControl.Draw(
            "ESC",
            "Exit",
            X,
            Height - 1,
            10,
            runContext.AppConfig.Theme,
            enabled: true,
            runContext.Terminal);
    }
    
    private void UpdateCpuRows(SystemSnapshot snapshot)
    {
        if (snapshot.Cpu == null) {
            return;
        }

        CpuSpecs specs = snapshot.Cpu.Specs;
        CpuMetrics metrics = snapshot.Cpu.Metrics;

        statsView.Items[2].SubItems[1].Text = specs.CpuName;
        statsView.Items[3].SubItems[1].Text = $"{specs.CpuCores} Cores";
#if __APPLE__
        statsView.Items[3].SubItems[1].Text +=
            $" · {specs.CpuEfficiencyCores} Efficiency · {specs.CpuPerformanceCores} Performance";
#endif
        statsView.Items[4].SubItems[1].Text = specs.ToCpuFrequencyGhz();

        statsView.Items[9].SubItems[1].Text = $"{metrics.CpuPercentKernelTime + metrics.CpuPercentUserTime:000.0%}";
        statsView.Items[10].SubItems[1].Text = $"{metrics.CpuPercentUserTime:000.0%}";
        statsView.Items[11].SubItems[1].Text = $"{metrics.CpuPercentKernelTime:000.0%}";
    }

    private void UpdateMemoryRows(SystemSnapshot snapshot)
    {
        if (snapshot.Memory == null) {
            return;
        }

        MemoryMetrics metrics = snapshot.Memory.Metrics;

        statsView.Items[6].SubItems[1].Text =
            (metrics.TotalPhysical - metrics.AvailablePhysical).ToFormattedByteSize() + "/" +
            metrics.TotalPhysical.ToFormattedByteSize();

        statsView.Items[7].SubItems[1].Text =
            (metrics.TotalPageFile - metrics.AvailablePageFile).ToFormattedByteSize() + "/" +
            metrics.TotalPageFile.ToFormattedByteSize();
    }

    private void UpdateGpuRows(SystemSnapshot snapshot)
    {
        if (snapshot.Gpu == null) {
            return;
        }

        GpuMetrics metrics = snapshot.Gpu.Metrics;

        statsView.Items[5].SubItems[1].Text = $"{snapshot.Gpu.Specs.GpuCores} Cores";
        statsView.Items[12].SubItems[1].Text = $"{metrics.GpuPercentTime:000.0%}";

        // Dedicated VRAM, matching Task Manager's "Dedicated GPU memory" rather than the combined
        // figure, which folds in host memory the adapter can address through its apertures.
        statsView.Items[13].SubItems[1].Text =
            (metrics.TotalGpuMemory - metrics.AvailableGpuMemory).ToFormattedByteSize() + "/" +
            metrics.TotalGpuMemory.ToFormattedByteSize();
    }

    private void UpdateNetworkRows(SystemSnapshot snapshot)
    {
        if (snapshot.Network == null) {
            return;
        }

        NetworkMetrics metrics = snapshot.Network.Metrics;

        statsView.Items[15].SubItems[1].Text =
            "↑ " + metrics.ToNetworkSendRate() + " " +
            "↓ " + metrics.ToNetworkReceiveRate();

        statsView.Items[16].SubItems[1].Text =
            "↑ " + $"{metrics.SendPacketsPerSecond:N0}" + " " +
            "↓ " + $"{metrics.ReceivePacketsPerSecond:N0}";
    }

    private void UpdateDiskRows(SystemSnapshot snapshot)
    {
        if (snapshot.Disk == null) {
            return;
        }

        DiskMetrics metrics = snapshot.Disk.Metrics;

        statsView.Items[18].SubItems[1].Text =
            (metrics.TotalBytesRead + metrics.TotalBytesWritten).ToString("N0");

        statsView.Items[19].SubItems[1].Text =
            metrics.ToDiskTransferBytesPerSecond().ToMbpsFromBytes() + " MB/s";
    }

    private void UpdateTopProcessRows(SystemSnapshot snapshot)
    {
        if (snapshot.Processes == null) {
            return;
        }

        ProcessEntry? topCpuAvgProc = null;
        ProcessEntry? topGpuAvgProc = null;
        ProcessEntry? topMemAvgProc = null;
        ProcessEntry? topDskAvgProc = null;

        ProcessEntry? topCpuMaxProc = null;
        ProcessEntry? topGpuMaxProc = null;
        ProcessEntry? topMemMaxProc = null;
        ProcessEntry? topDskMaxProc = null;

        List<ProcessEntry> entries = snapshot.Processes.Metrics.Entries;

        for (int i = 0; i < entries.Count; i++) {
            ProcessEntry p = entries[i];

            if (topCpuAvgProc is null || p.CpuTimePercentAvg     > topCpuAvgProc.CpuTimePercentAvg)     topCpuAvgProc = p;
            if (topGpuAvgProc is null || p.GpuTimePercentAvg     > topGpuAvgProc.GpuTimePercentAvg)     topGpuAvgProc = p;
            if (topMemAvgProc is null || p.UsedMemoryAvg         > topMemAvgProc.UsedMemoryAvg)         topMemAvgProc = p;
            if (topDskAvgProc is null || p.DiskBytesPerSecondAvg > topDskAvgProc.DiskBytesPerSecondAvg) topDskAvgProc = p;

            if (topCpuMaxProc is null || p.CpuTimePercentMax     > topCpuMaxProc.CpuTimePercentMax)     topCpuMaxProc = p;
            if (topGpuMaxProc is null || p.GpuTimePercentMax     > topGpuMaxProc.GpuTimePercentMax)     topGpuMaxProc = p;
            if (topMemMaxProc is null || p.UsedMemoryMax         > topMemMaxProc.UsedMemoryMax)         topMemMaxProc = p;
            if (topDskMaxProc is null || p.DiskBytesPerSecondMax > topDskMaxProc.DiskBytesPerSecondMax) topDskMaxProc = p;
        }

        statsView.Items[21].SubItems[1].Text = topCpuAvgProc is null ? string.Empty : $"{topCpuAvgProc.CpuTimePercentAvg:000.0%} Pid {topCpuAvgProc.Pid} {topCpuAvgProc.FileDescription}";
        statsView.Items[22].SubItems[1].Text = topGpuAvgProc is null ? string.Empty : $"{topGpuAvgProc.GpuTimePercentAvg:000.0%} Pid {topGpuAvgProc.Pid} {topGpuAvgProc.FileDescription}";
        statsView.Items[23].SubItems[1].Text = topMemAvgProc is null ? string.Empty : $"{topMemAvgProc.UsedMemoryAvg.ToFormattedByteSize()} Pid {topMemAvgProc.Pid} {topMemAvgProc.FileDescription}";
        statsView.Items[24].SubItems[1].Text = topDskAvgProc is null ? string.Empty : $"{topDskAvgProc.DiskBytesPerSecondAvg.ToMbpsFromBytes()} MB/s Pid {topDskAvgProc.Pid} {topDskAvgProc.FileDescription}";

        statsView.Items[26].SubItems[1].Text = topCpuMaxProc is null ? string.Empty : $"{topCpuMaxProc.CpuTimePercentMax:000.0%} Pid {topCpuMaxProc.Pid} {topCpuMaxProc.FileDescription}";
        statsView.Items[27].SubItems[1].Text = topGpuMaxProc is null ? string.Empty : $"{topGpuMaxProc.GpuTimePercentMax:000.0%} Pid {topGpuMaxProc.Pid} {topGpuMaxProc.FileDescription}";
        statsView.Items[28].SubItems[1].Text = topMemMaxProc is null ? string.Empty : $"{topMemMaxProc.UsedMemoryMax.ToFormattedByteSize()} Pid {topMemMaxProc.Pid} {topMemMaxProc.FileDescription}";
        statsView.Items[29].SubItems[1].Text = topDskMaxProc is null ? string.Empty : $"{topDskMaxProc.DiskBytesPerSecondMax.ToMbpsFromBytes()} MB/s Pid {topDskMaxProc.Pid} {topDskMaxProc.FileDescription}";
    }

    protected override void OnDraw() => DrawInternal();

    protected override void OnLoad()
    {
        for (int i = 0; i < Art.Length; i++) {
            colors[i] = Art[i].hex;
        }

        BackgroundColour = runContext.AppConfig.Theme.Background;
        ForegroundColour = runContext.AppConfig.Theme.Foreground;
        
        statsView.BackgroundColour = runContext.AppConfig.Theme.ListViewBackground;
        statsView.ForegroundColour = runContext.AppConfig.Theme.ListViewForeground;
        statsView.BorderForegroundColour = runContext.AppConfig.Theme.ListViewBorderForeground;
        statsView.BorderBackgroundColour = runContext.AppConfig.Theme.ListViewBorderBackground;
        
        statsView.Items.Add(new ListViewItem(new[] { "Machine:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Operating System:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "CPU:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "CPU Cores:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Frequency:", ""}));
        statsView.Items.Add(new ListViewItem(new[] { "GPU:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Memory:", "" }));
#if __APPLE__        
        statsView.Items.Add(new ListViewItem(new[] { "Swap:", "" }));
#elif __WIN32__
        statsView.Items.Add(new ListViewItem(new[] { "Virtual:", "" }));
#endif
        statsView.Items.Add(new ListViewItem(new[] { "", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "CPU Usage:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "CPU User Usage:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "CPU Kernel Usage:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "GPU Usage:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "GPU Memory:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Network Bytes:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Network Packets:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Disk R+W Total Bytes:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Disk Usage:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Top CPU Avg:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Top GPU Avg:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Top Memory Avg:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Top Disk Avg:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Max CPU:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Max GPU:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Max Memory:", "" }));
        statsView.Items.Add(new ListViewItem(new[] { "Max Disk:", "" }));

        runContext.Terminal.CursorVisible = false;
        runContext.ServiceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
        
        base.OnLoad();
    }

    protected override void OnResize()
    {
        runContext.Terminal.BackgroundColor = runContext.AppConfig.Theme.Background;

        statsView.Y = Y + 3;
        statsView.X = X + Art.Max(arr => arr.text.Length) + 12;
        statsView.Width = runContext.Terminal.WindowWidth - statsView.X - 2;
        statsView.Height = statsView.Items.Count + 1;

        statsView.ColumnHeaders[0].Width = 25;
        statsView.ColumnHeaders[1].Width = statsView.Width - statsView.ColumnHeaders[0].Width;
        
        base.OnResize();
    }

    protected override void OnUnload()
    {
        statsView.Items.Clear();
        
        runContext.Terminal.CursorVisible = true;
        runContext.ServiceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        
        base.OnUnload();
    }
    
    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        snapshot = e.Snapshot;
        Draw();
    }
}