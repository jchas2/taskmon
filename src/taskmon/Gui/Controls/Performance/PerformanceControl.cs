using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Npu;

namespace Task.Monitor.Gui.Controls.Performance;

public sealed class PerformanceControl : Control
{
    private const int EntryHeight    = 7;
    private const int MaxColumnWidth = 42;
    private const int SparkWidth     = 14;

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly AnsiScreenBuffer  frame = new();

    private List<Control> details = [];
    private Control       selected;

    private int columnWidth;
    private int scrollOffset;
    private int visibleCount;

    public PerformanceControl(
        ServiceController serviceController,
        ISystemTerminal   terminal,
        AppConfig         appConfig) : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        details  = DetailsFor(snapshot: null);
        selected = details[0];

        foreach (Control detail in details) {
            Controls.Add(detail);
        }
    }

    internal IReadOnlyList<Control> Details => details;

    private List<Control> DetailsFor(SystemSnapshot? snapshot)
    {
        T Detail<T>(Func<T, bool> same, Func<T> create) where T : Control =>
            details.OfType<T>().FirstOrDefault(same) ?? create();

        List<Control> wanted = [
            Detail<CpuPerformanceControl>(_ => true, () => new(Terminal, appConfig)),
            Detail<MemoryPerformanceControl>(_ => true, () => new(Terminal, appConfig))
        ];

        IEnumerable<long?>  gpus     = snapshot?.Gpu?.Metrics.Devices.Select(device => (long?)device.AdapterLuid) ?? [];
        IEnumerable<int?>   disks    = snapshot?.Disk?.Metrics.Devices.Select(device => (int?)device.Index) ?? [];
        IEnumerable<ulong?> adapters = snapshot?.Network?.Metrics.Devices.Select(device => (ulong?)device.InterfaceLuid) ?? [];

        foreach (long? luid in gpus.DefaultIfEmpty()) {
            wanted.Add(
                Detail<GpuPerformanceControl>(
                    gpu => gpu.AdapterLuid == luid, 
                    () => new(Terminal, appConfig, luid)));
        }

        foreach (NpuDevice npu in snapshot?.Npu?.Specs.Devices ?? []) {
            wanted.Add(
                Detail<NpuPerformanceControl>(
                    detail => detail.Index == npu.Index, 
                    () => new(Terminal, appConfig, npu.Index)));
        }

        foreach (int? index in disks.DefaultIfEmpty()) {
            wanted.Add(
                Detail<DiskPerformanceControl>(
                    disk => disk.DiskIndex == index, 
                    () => new(Terminal, appConfig, index)));
        }

        foreach (ulong? luid in adapters.DefaultIfEmpty()) {
            wanted.Add(
                Detail<NetworkPerformanceControl>(
                    network => network.InterfaceLuid == luid, 
                    () => new(Terminal, appConfig, luid)));
        }

        return wanted;
    }

    private void LoadDetail(Control detail)
    {
        detail.Load();

        Chart spark = ((IPerformanceDetail)detail).Spark;

        CommonControl.Theme(spark, appConfig.Theme);

        spark.MetreStyle     = appConfig.MetreStyle;
        spark.ShowGrid       = appConfig.ShowSmallMetreGrid;
        spark.ShowYAxisScale = appConfig.ShowYAxisScale;
    }

    protected override void OnDraw()
    {
        Theme theme     = appConfig.Theme;
        int   textWidth = columnWidth - SparkWidth;
        int   shown     = Math.Min(visibleCount, details.Count - scrollOffset);
        Color border    = Focused ? FocusSelectionColour : theme.ChartBorderForeground;

        for (int row = 0; row < shown; row++) {
            Control            control = details[scrollOffset + row];
            IPerformanceDetail detail  = (IPerformanceDetail)control;
            int                y       = Y + row * EntryHeight;

            frame.Clear();

            if (control == selected) {
                Color foreground = Focused ? theme.ForegroundHighlight : theme.ForegroundHighlightInactive;
                Color background = Focused ? theme.BackgroundHighlight : theme.BackgroundHighlightInactive;

                DrawRectangleWithBevel(
                    X, 
                    y, 
                    textWidth, 
                    EntryHeight, background);

                frame.SetColour(foreground, background);
                frame.MoveTo(X + 1, y + 1);
                frame.Append(detail.Title.PadOrTruncate(' ', textWidth - 2));
            }
            else {
                Color strip = theme.PerformancePanelTitlebarBackground;

                DrawRectangle(
                    X, 
                    y, 
                    textWidth, 
                    EntryHeight, 
                    BackgroundColour);
                
                DrawHorizontalLine(
                    y, 
                    X, 
                    X + textWidth, 
                    ForegroundColour);
                
                DrawHorizontalLine(
                    y + EntryHeight - 1, 
                    X, 
                    X + textWidth, 
                    ForegroundColour);

                frame.MoveTo(X, y + 1);
                
                AppendHalfBlock(
                    frame, 
                    left: BackgroundColour, 
                    right: strip, 
                    fallback: strip);
                
                frame.SetColour(theme.PerformancePanelTitlebarForeground, strip);
                frame.Append(detail.Title.PadOrTruncate(' ', textWidth - 2));
                
                AppendHalfBlock(
                    frame, 
                    left: strip, 
                    right: BackgroundColour, 
                    fallback: strip);
                
                frame.SetColour(ForegroundColour, BackgroundColour);
            }

            frame.MoveTo(X + 1, y + 3);
            frame.Append(detail.Summary.Top);
            frame.MoveTo(X + 1, y + 4);
            frame.Append(detail.Summary.Bottom);
            frame.ResetColour();
            Terminal.Write(frame.AsSpan());

            detail.Spark.Draw();
        }

        DrawRectangle(
            X, 
            Y + shown * EntryHeight, 
            columnWidth, 
            Height - shown * EntryHeight, 
            BackgroundColour);
        
        DrawVerticalLine(
            X + columnWidth, 
            Y, 
            Y + Height, 
            border);

        void Arrow(int y, char glyph)
        {
            frame.Clear();
            frame.MoveTo(X + columnWidth, y);
            frame.SetColour(border, BackgroundColour);
            frame.Append(glyph);
            frame.ResetColour();
            Terminal.Write(frame.AsSpan());
        }

        if (scrollOffset > 0) {
            Arrow(Y, '▲');
        }

        if (scrollOffset + visibleCount < details.Count) {
            Arrow(Y + visibleCount * EntryHeight - 1, '▼');
        }

        selected.Draw();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        int current = details.IndexOf(selected);
        
        int next = keyInfo.Key switch {
            ConsoleKey.UpArrow   => current - 1,
            ConsoleKey.DownArrow => current + 1,
                               _ => current
        };

        if (next == current || next < 0 || next >= details.Count) {
            return;
        }

        selected = details[next];
        selected.Clear();
        handled = true;

        Resize();
        Draw();
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);

        BackgroundColour = appConfig.Theme.PerformancePanelBackground;
        ForegroundColour = appConfig.Theme.PerformancePanelForeground;

        foreach (Control detail in details) {
            LoadDetail(detail);
        }

        selected     = details[0];
        scrollOffset = 0;

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
    }

    protected override void OnResize()
    {
        columnWidth  = Math.Min(MaxColumnWidth, (int)(Width * 0.3));
        visibleCount = Math.Max(1, Height / EntryHeight);

        int index = details.IndexOf(selected);

        scrollOffset = Math.Clamp(scrollOffset, index - visibleCount + 1, index);
        scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, details.Count - visibleCount));

        for (int i = 0; i < details.Count; i++) {
            Chart spark = ((IPerformanceDetail)details[i]).Spark;

            spark.Visible = i >= scrollOffset && i < scrollOffset + visibleCount;
            spark.X       = X + columnWidth - SparkWidth;
            spark.Y       = Y + (i - scrollOffset) * EntryHeight;
            spark.Width   = SparkWidth;
            spark.Height  = EntryHeight;
            spark.Resize();

            details[i].X      = X + columnWidth;
            details[i].Y      = Y;
            details[i].Width  = Width - columnWidth;
            details[i].Height = Height;
        }

        base.OnResize();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        lock (DrawingLock) {
            List<Control> wanted = DetailsFor(e.Snapshot);

            if (!wanted.SequenceEqual(details)) {
                foreach (Control gone in details.Except(wanted)) {
                    gone.Unload();
                    Controls.Remove(gone);
                }

                foreach (Control added in wanted.Except(details)) {
                    Controls.Add(added);
                    LoadDetail(added);
                }

                selected = wanted.Contains(selected)
                    ? selected
                    : wanted.FirstOrDefault(detail => detail.GetType() == selected.GetType()) ?? wanted[0];

                details      = wanted;
                scrollOffset = 0;

                Clear();
                Resize();
            }

            foreach (Control detail in details) {
                ((IPerformanceDetail)detail).Sample(e.Snapshot);
            }

            Draw();
        }
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        base.OnUnload();
    }

    internal Control Selected => selected;
}
