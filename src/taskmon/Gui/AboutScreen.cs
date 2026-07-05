using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Process;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Process;
using Task.Monitor.System.Screens;
using WorkerTask = System.Threading.Tasks.Task;

namespace Task.Monitor.Gui;

public sealed class AboutScreen : Screen
{
    #if __APPLE__
    private static readonly (string text, string hex)[] Art =
    {
        ("                  .,o",                       "3CC846"), // Green.
        ("                 /gg,",                       "3CC846"), 
        ("              (dMMb",                         "3CC846"), 
        ("               .o,",                          "3CC846"), 
        ("    .gggMbgg.     .,ggMMg,",                  "3CC846"), 
        ("   dMMMMMMMMMMMMMMMMMMMMMMb",                 "E63C32"), // Red.
        ("  dMMMMMMMMMMMMMMMMMMMMMMMMb",                "E63C32"),
        (" dMMMMMMMMMMMMMMMMMMMMMMMMMMb",               "F08C1E"), // Orange.
        (".MMMMMMMMMMMMMMMMMMMMMMMMMb,",                "F08C1E"), 
        ("MMMMMMMMMMMMMMMMMMMMMMMMM`",                  "EBD228"), // Yellow.
        ("MMMMMMMMMMMMMMMMMMMMMMMM`",                   "EBD228"), 
        ("MMMMMMMMMMMMMMMMMMMMMMMM,",                   "3CC846"), // Green.
        ("MMMMMMMMMMMMMMMMMMMMMMMMM.",                  "3CC846"), 
        (".MMMMMMMMMMMMMMMMMMMMMMMMMM'",                "28C8D2"), // Cyan.
        (" `MMMMMMMMMMMMMMMMMMMMMMMMMMd'",              "28C8D2"),
        ("  `bMMMMMMMMMMMMMMMMMMMMMMMMd'",              "326EE6"), // Blue.
        ("   `bMMMMMMMMMTASKMMMMMMMMMd'",               "326EE6"),
        ("     `MbMMMMMMONITORMMMMMdM'",                "BE46C8"), // Magenta.
        ("       `MMbgg,,,,,,,ggdMM'",                  "BE46C8"),
        ("         `''        ''`",                     "BE46C8"),
    };
#endif
#if __WIN32__
    private static readonly (string text, string hex)[] Art =
    {
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "FFFFFF"), // White.
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "F2F8FD"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "E4F1FA"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "D7EAF8"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "C9E3F6"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "BCDCF4"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "AED4F1"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "A1CDEF"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "94C6ED"),
        ("",                                        "79B8E8"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "6BB1E6"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "5EAAE4"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "51A3E2"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "439CDF"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "3694DD"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "288DDB"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "1B86D9"),
        ("MMMMMMMMMMMM  MMMMMMMMMMMM",              "0D7FD6"),
        ("MMMMMMMMTASK  MONITORMMMMM",              "0078D4"), // Blue.
    };    
#endif
    
    private string[] colors = new string[Art.Length];
    private readonly RunContext runContext;
    private ProcessorEventArgs? eventArgs;
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
            runContext.AppConfig.DefaultTheme.MenubarForeground, 
            runContext.AppConfig.DefaultTheme.MenubarBackground);
        
        int offsetX = Terminal.WindowWidth / 2 - menubar.Length / 2;

        frame.Append(' ', offsetX);
        frame.Append(menubar.ToBold());
        frame.Append(' ', Width - offsetX - menubar.Length);
        
        frame.SetColour(
            runContext.AppConfig.DefaultTheme.Foreground, 
            runContext.AppConfig.DefaultTheme.Background);
        
        offsetX = Terminal.WindowWidth / 2 - version.Length / 2;
        
        frame.Append(' ', offsetX);
        frame.Append(version.ToBold());
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

        if (eventArgs == null) {
            return;
        }

        statsView.Items[0].SubItems[1].Text = eventArgs.Statistics.MachineName;
        statsView.Items[1].SubItems[1].Text = eventArgs.Statistics.OsVersion;
        statsView.Items[2].SubItems[1].Text = eventArgs.Statistics.CpuName;
        statsView.Items[3].SubItems[1].Text = $"{eventArgs.Statistics.CpuCores} Cores";
#if __APPLE__
        statsView.Items[3].SubItems[1].Text +=
            $" · {eventArgs.Statistics.CpuEfficiencyCores} Efficiency · {eventArgs.Statistics.CpuPerformanceCores} Performance";
#endif
        statsView.Items[4].SubItems[1].Text = $"{eventArgs.Statistics.CpuFrequency / 1000.0:0.00} GHz";
        statsView.Items[5].SubItems[1].Text = $"{eventArgs.Statistics.GpuCores} Cores";
        statsView.Items[6].SubItems[1].Text =
            (eventArgs.Statistics.TotalPhysical - eventArgs.Statistics.AvailablePhysical).ToFormattedByteSize() + "/" +
            eventArgs.Statistics.TotalPhysical.ToFormattedByteSize();
        statsView.Items[7].SubItems[1].Text =
            (eventArgs.Statistics.TotalPageFile - eventArgs.Statistics.AvailablePageFile).ToFormattedByteSize() + "/" +
            eventArgs.Statistics.TotalPageFile.ToFormattedByteSize();

        statsView.Items[9].SubItems[1].Text = $"{eventArgs.Statistics.CpuPercentKernelTime + eventArgs.Statistics.CpuPercentUserTime:000.0%}";
        statsView.Items[10].SubItems[1].Text = $"{eventArgs.Statistics.CpuPercentUserTime:000.0%}";
        statsView.Items[11].SubItems[1].Text = $"{eventArgs.Statistics.CpuPercentKernelTime:000.0%}";
        statsView.Items[12].SubItems[1].Text = $"{eventArgs.Statistics.GpuPercentTime:000.0%}";
        statsView.Items[13].SubItems[1].Text =
            (eventArgs.Statistics.TotalGpuMemory - eventArgs.Statistics.AvailableGpuMemory).ToFormattedByteSize() + "/" +
            eventArgs.Statistics.TotalGpuMemory.ToFormattedByteSize();

        statsView.Items[15].SubItems[1].Text = 
            "\u2191 " + eventArgs.Statistics.NetworkBytesSendTime.ToFormattedByteSize() + " " +
            "\u2193 " + eventArgs.Statistics.NetworkBytesReceiveTime.ToFormattedByteSize();
        statsView.Items[16].SubItems[1].Text = 
            "\u2191 " + eventArgs.Statistics.NetworkPacketsSendTime + " " +
            "\u2193 " + eventArgs.Statistics.NetworkPacketsReceiveTime;

        statsView.Items[18].SubItems[1].Text =
            (eventArgs.Statistics.TotalDiskReadBytes + eventArgs.Statistics.TotalDiskWriteBytes).ToString("N0");
        statsView.Items[19].SubItems[1].Text = eventArgs.Statistics.DiskUsage.ToMbpsFromBytes() + " MB/s";

        ProcessorInfo? topCpuAvgProc = null;
        ProcessorInfo? topGpuAvgProc = null;
        ProcessorInfo? topMemAvgProc = null;
        ProcessorInfo? topDskAvgProc = null;

        ProcessorInfo? topCpuMaxProc = null;
        ProcessorInfo? topGpuMaxProc = null;
        ProcessorInfo? topMemMaxProc = null;
        ProcessorInfo? topDskMaxProc = null;

        List<ProcessorInfo> processInfos = eventArgs.ProcessInfos;

        for (int i = 0; i < processInfos.Count; i++) {
            ProcessorInfo p = processInfos[i];

            if (topCpuAvgProc is null || p.CpuTimePercentAvg > topCpuAvgProc.CpuTimePercentAvg) topCpuAvgProc = p;
            if (topGpuAvgProc is null || p.GpuTimePercentAvg > topGpuAvgProc.GpuTimePercentAvg) topGpuAvgProc = p;
            if (topMemAvgProc is null || p.UsedMemoryAvg     > topMemAvgProc.UsedMemoryAvg)     topMemAvgProc = p;
            if (topDskAvgProc is null || p.DiskUsageAvg      > topDskAvgProc.DiskUsageAvg)      topDskAvgProc = p;

            if (topCpuMaxProc is null || p.CpuTimePercentMax > topCpuMaxProc.CpuTimePercentMax) topCpuMaxProc = p;
            if (topGpuMaxProc is null || p.GpuTimePercentMax > topGpuMaxProc.GpuTimePercentMax) topGpuMaxProc = p;
            if (topMemMaxProc is null || p.UsedMemoryMax     > topMemMaxProc.UsedMemoryMax)     topMemMaxProc = p;
            if (topDskMaxProc is null || p.DiskUsageMax      > topDskMaxProc.DiskUsageMax)      topDskMaxProc = p;
        }

        statsView.Items[21].SubItems[1].Text = topCpuAvgProc is null ? string.Empty : $"{topCpuAvgProc.CpuTimePercentAvg:000.0%} Pid {topCpuAvgProc.Pid} {topCpuAvgProc.FileDescription}";
        statsView.Items[22].SubItems[1].Text = topGpuAvgProc is null ? string.Empty : $"{topGpuAvgProc.GpuTimePercentAvg:000.0%} Pid {topGpuAvgProc.Pid} {topGpuAvgProc.FileDescription}";
        statsView.Items[23].SubItems[1].Text = topMemAvgProc is null ? string.Empty : $"{topMemAvgProc.UsedMemoryAvg.ToFormattedByteSize()} Pid {topMemAvgProc.Pid} {topMemAvgProc.FileDescription}";
        statsView.Items[24].SubItems[1].Text = topDskAvgProc is null ? string.Empty : $"{topDskAvgProc.DiskUsageAvg.ToMbpsFromBytes()} MB/s Pid {topDskAvgProc.Pid} {topDskAvgProc.FileDescription}";

        statsView.Items[26].SubItems[1].Text = topCpuMaxProc is null ? string.Empty : $"{topCpuMaxProc.CpuTimePercentMax:000.0%} Pid {topCpuMaxProc.Pid} {topCpuMaxProc.FileDescription}";
        statsView.Items[27].SubItems[1].Text = topGpuMaxProc is null ? string.Empty : $"{topGpuMaxProc.GpuTimePercentMax:000.0%} Pid {topGpuMaxProc.Pid} {topGpuMaxProc.FileDescription}";
        statsView.Items[28].SubItems[1].Text = topMemMaxProc is null ? string.Empty : $"{topMemMaxProc.UsedMemoryMax.ToFormattedByteSize()} Pid {topMemMaxProc.Pid} {topMemMaxProc.FileDescription}";
        statsView.Items[29].SubItems[1].Text = topDskMaxProc is null ? string.Empty : $"{topDskMaxProc.DiskUsageMax.ToMbpsFromBytes()} MB/s Pid {topDskMaxProc.Pid} {topDskMaxProc.FileDescription}";
        
        statsView.Draw();

        KeyBindControl.Draw(
            "ESC",
            "Exit",
            X,
            Height - 1,
            10,
            runContext.AppConfig.DefaultTheme,
            enabled: true,
            runContext.Terminal);
    }
    
    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();
            DrawInternal();    
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    protected override void OnLoad()
    {
        for (int i = 0; i < Art.Length; i++) {
            colors[i] = Art[i].hex;
        }

        BackgroundColour = runContext.AppConfig.DefaultTheme.Background;
        ForegroundColour = runContext.AppConfig.DefaultTheme.Foreground;
        
        statsView.BackgroundColour = BackgroundColour;
        statsView.ForegroundColour = ForegroundColour;
        
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
        runContext.Processor.ProcessorUpdated += ProcessorOnProcessorUpdated;
        
        base.OnLoad();
    }

    protected override void OnResize()
    {
        runContext.Terminal.BackgroundColor = runContext.AppConfig.DefaultTheme.Background;

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
        runContext.Processor.ProcessorUpdated -= ProcessorOnProcessorUpdated;
        
        base.OnUnload();
    }
    
    private void ProcessorOnProcessorUpdated(object? sender, ProcessorEventArgs e)
    {
        eventArgs = e;
        Draw();
    }
}