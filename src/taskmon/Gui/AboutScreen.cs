using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Gui.Controls.Mascot;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Screens;
using Task.Monitor.System.Services;
using SysThreading = System.Threading;

namespace Task.Monitor.Gui;

// Top to bottom, centred in the height: the robot, the sampling engine's figures, and its samplers.
// Each shows only when it fits whole, in that order; without room for the robot, its caption alone.
public sealed class AboutScreen : Screen
{
    private const int    ColumnGap    = 3;
    private const string Dot          = "●";
    private const int    DotWidth     = 2;
    private const int    HeadingRows  = 2;
    private const int    HintWidth    = 10;
    private const int    LabelGap     = 2;
    private const string Missing      = "—";
    private const int    ReservedRows = 2;
    private const int    SectionGap   = 1;
    private const int    ValueWidth   = 16;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    private static readonly DateTime StartedUtc = ProcessStartUtc();

    private readonly AnsiScreenBuffer frame = new();
    private readonly bool             isElevated;
    private readonly MascotControl    mascot;
    private readonly RunContext       runContext;
    private readonly ListView         samplersView;
    private readonly ListView         statsView;

    private string?                      copyMessage;
    private int                          headingY;
    private DateTime?                    lastSampleUtc;
    private SysThreading::Timer?         refreshTimer;
    private IReadOnlyList<ServiceHealth> samplers = [];
    private long?                        samples;
    private int                          statsPairs = 2;

    public AboutScreen(RunContext runContext) : base(runContext.Terminal)
    {
        this.runContext = runContext;

        isElevated = SystemInfo.IsRunningAsRoot();

        mascot = new MascotControl(runContext.Terminal, MascotAnimation.LoadEmbedded()) {
            Caption = [
                Strings.AboutProductName,
                string.Format(CultureInfo.CurrentCulture, Strings.AboutVersion, AssemblyVersionInfo.GetVersion()),
                RunningAsText(isElevated)
            ],
            Visible = true
        };

        statsView    = CreateTable(runContext.Terminal);
        samplersView = CreateTable(runContext.Terminal);

        Controls.Add(mascot);
        Controls.Add(statsView);
        Controls.Add(samplersView);
    }

    private ListViewSubItem Cell(string text, Color foreground) =>
        new(null!, text, runContext.AppConfig.Theme.Background, foreground);

    private static string ColoursText()
    {
        string mode = ConsolePalette.PreferIndexedColours ? Strings.AboutColoursIndexed : Strings.AboutColoursTrue;

        return TerminalCapabilities.IsColorModeOverriddenByEnvironment(Environment.GetEnvironmentVariable)
            ? string.Format(CultureInfo.CurrentCulture, Strings.AboutColoursFromEnvironment, mode, TerminalCapabilities.ColourModeEnvVar)
            : mode;
    }

    private void CopyDetails()
    {
        AppConfig     appConfig = runContext.AppConfig;
        StringBuilder text      = new();

        // A heading, then each row indented as "label: value", or just the value without a label.
        void Section(string heading, IEnumerable<(string Label, string Value)> rows)
        {
            text.Append(heading).Append('\n');

            foreach ((string label, string value) in rows) {
                text.Append("  ").Append(label.Length > 0 ? $"{label}: " : string.Empty).Append(value).Append('\n');
            }

            text.Append('\n');
        }

        Section(Strings.AboutProductName, [
            (Strings.AboutLabelVersion, AssemblyVersionInfo.GetVersion()),
            (Strings.AboutLabelRuntime, RuntimeText()),
            (Strings.AboutLabelSystem,  SystemInfo.GetOsVersion()),
            (string.Empty,              RunningAsText(isElevated))
        ]);

        Section(Strings.AboutSectionTerminal, [
            (Strings.AboutLabelSize,     $"{Width} × {Height}"),
            (Strings.AboutLabelColours,  ColoursText()),
            (Strings.AboutLabelDrawing,  DrawingText()),
            (Strings.FooterKeyTheme,     appConfig.Theme.Name),
            (Strings.AboutLabelLanguage, CultureInfo.CurrentUICulture.NativeName)
        ]);

        Section(Strings.AboutSamplingEngine, [
            .. EngineRows(),
            .. samplers.Select(sampler => (SamplerName(sampler.Name), sampler.Status.ToDisplayText()))
        ]);

        Section(Strings.AboutSectionFiles, [
            (Strings.AboutLabelSettings,      appConfig.DefaultConfigPath ?? Missing),
            (Strings.AboutLabelHealthHistory, appConfig.DefaultEventsPath ?? Missing),
            (Strings.AboutLabelLogs,          AppConfig.GetDefaultLogPath(runContext.FileSystem) ?? Missing)
        ]);

        copyMessage = CopyToClipboard(text.ToString().TrimEnd('\n') + "\n") ? Strings.AboutCopied : Strings.AboutCopyFailed;
        DrawHints();
    }

    internal Func<string, bool> CopyToClipboard { get; set; } = SystemClipboard.SetText;

    private static ListView CreateTable(ISystemTerminal terminal) =>
        new(terminal) {
            EnableRowSelect   = false,
            EnableScroll      = false,
            ShowBorder        = false,
            ShowColumnHeaders = false,
            TabStop           = false,
            Visible           = false
        };

    private void DrawHints()
    {
        Theme theme = runContext.AppConfig.Theme;
        int   y     = Y + Height - 1;
        int   x     = X;

        x += KeyBindControl.Draw("ESC", Strings.KeyBindExit,  x, y, HintWidth, theme, enabled: true, Terminal) + 1;
        x += KeyBindControl.Draw("C",   Strings.AboutKeyCopy, x, y, HintWidth, theme, enabled: true, Terminal) + 1;

        int room = X + Width - x;

        if (room <= 0) {
            return;
        }

        string message = copyMessage ?? string.Empty;

        frame.Clear();
        frame.MoveTo(x, y);
        frame.SetColour(theme.Foreground, theme.Background);
        frame.Append(' ');
        frame.Append(message.AsSpan(0, Math.Min(message.Length, room - 1)));
        frame.Append(' ', Math.Max(0, room - 1 - message.Length));
        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }

    private static string DrawingText() =>
        !DrawingCapabilities.HasFlag(DrawingCapabilities.QuadrantGlyphs)      ? Strings.AboutDrawingSquareCorners
      : !DrawingCapabilities.HasFlag(DrawingCapabilities.SeamlessBlockGlyphs) ? Strings.AboutDrawingWholeCells
                                                                              : Strings.AboutDrawingFull;

    private void DrawLine(int y, string text, Color foreground, Color background)
    {
        int left  = Math.Max(0, (Width - text.Length) / 2);
        int shown = Math.Min(text.Length, Width - left);

        frame.Clear();
        frame.MoveTo(X, y);
        frame.SetColour(foreground, background);
        frame.Append(' ', left);
        frame.Append(text.AsSpan(0, shown));
        frame.Append(' ', Width - left - shown);
        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }

    private (string Label, string Value)[] EngineRows()
    {
        TimeSpan? age = lastSampleUtc is { } sampled ? DateTime.UtcNow - sampled : null;

        string ago = age is not { } value ? Missing
            : string.Format(CultureInfo.CurrentCulture, Strings.AboutAgo, value.TotalMinutes < 1.0
                ? string.Format(CultureInfo.CurrentCulture, Strings.HealthDurationSeconds, Math.Round(value.TotalSeconds, 1))
                : value.ToDurationText());

        return [
            (Strings.AboutLabelUptime,     (DateTime.UtcNow - StartedUtc).ToDurationText()),
            (Strings.AboutLabelInterval,   string.Format(CultureInfo.CurrentCulture, Strings.HealthDurationSeconds, runContext.ServiceController.Delay / 1000.0)),
            (Strings.AboutLabelSamples,    samples?.ToString("N0", CultureInfo.CurrentCulture) ?? Missing),
            (Strings.AboutLabelLastSample, ago)
        ];
    }

    private void FillSamplers(int columns, int rows, int nameWidth)
    {
        Theme theme = runContext.AppConfig.Theme;

        samplersView.ColumnHeaders.Clear();

        for (int column = 0; column < columns; column++) {
            samplersView.ColumnHeaders
                .Add(new ListViewColumnHeader(string.Empty) { Width = DotWidth })
                .Add(new ListViewColumnHeader(string.Empty) { Width = nameWidth });
        }

        samplersView.Items.Clear();

        for (int row = 0; row < rows; row++) {
            List<ListViewSubItem> cells = [];

            for (int column = 0; column < columns; column++) {
                int index = column * rows + row;

                if (index >= samplers.Count) {
                    cells.Add(Cell(string.Empty, theme.Foreground));
                    cells.Add(Cell(string.Empty, theme.Foreground));
                    continue;
                }

                ServiceHealth sampler = samplers[index];

                Color dot = sampler.Status switch {
                    ServiceStatus.Running => theme.StatusbarStatusOk,
                    ServiceStatus.Errored => theme.StatusbarStatusError,
                                        _ => theme.StatusbarStatusWarning
                };

                cells.Add(Cell(Dot, dot));
                cells.Add(Cell(SamplerText(sampler), theme.Foreground));
            }

            samplersView.Items.Add(new ListViewItem([.. cells]));
        }
    }

    private void FillStats()
    {
        Theme                         theme = runContext.AppConfig.Theme;
        (string Label, string Value)[] rows = EngineRows();

        int labelWidth = rows.Max(row => row.Label.Length) + LabelGap;
        int lines      = rows.Length / statsPairs;

        statsView.ColumnHeaders.Clear();

        for (int pair = 0; pair < statsPairs; pair++) {
            statsView.ColumnHeaders
                .Add(new ListViewColumnHeader(string.Empty) { Width = labelWidth })
                .Add(new ListViewColumnHeader(string.Empty) { Width = ValueWidth });
        }

        statsView.Items.Clear();

        for (int line = 0; line < lines; line++) {
            List<ListViewSubItem> cells = [];

            for (int pair = 0; pair < statsPairs; pair++) {
                (string label, string value) = rows[pair * lines + line];

                cells.Add(Cell(label, theme.PropertyKey));
                cells.Add(Cell(value, theme.PropertyValue));
            }

            statsView.Items.Add(new ListViewItem([.. cells]));
        }
    }

    protected override void OnDraw()
    {
        Theme theme = runContext.AppConfig.Theme;

        DrawLine(Y, Title, theme.MenubarForeground, theme.MenubarBackground);
        mascot.Draw();

        if (statsView.Visible) {
            DrawLine(headingY, Strings.AboutSamplingEngine, theme.PropertyKey, theme.Background);
            statsView.Draw();
        }

        samplersView.Draw();
        DrawHints();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        if (mascot.IsIntroPlaying && keyInfo.Key is not (ConsoleKey.Escape or ConsoleKey.F10)) {
            mascot.Skip();
            handled = true;
            return;
        }

        if (keyInfo.Key == ConsoleKey.C) {
            CopyDetails();
            handled = true;
            return;
        }

        base.OnKeyPressed(keyInfo, ref handled);
    }

    protected override void OnLoad()
    {
        Theme theme = runContext.AppConfig.Theme;

        BackgroundColour        = theme.Background;
        ForegroundColour        = theme.Foreground;
        mascot.BackgroundColour = theme.Background;
        mascot.ForegroundColour = theme.Foreground;
        mascot.TitleColour      = theme.PropertyKey;

        statsView.BackgroundColour    = theme.Background;
        statsView.ForegroundColour    = theme.Foreground;
        samplersView.BackgroundColour = theme.Background;
        samplersView.ForegroundColour = theme.Foreground;

        samplers = runContext.ServiceController.GetServiceHealth();

        runContext.Terminal.CursorVisible = false;
        runContext.ServiceController.SystemSnapshotUpdated += OnSnapshotUpdated;

        refreshTimer = new SysThreading::Timer(OnRefreshTick, null, RefreshInterval, RefreshInterval);

        base.OnLoad();
    }

    internal void OnRefreshTick(object? state)
    {
        lock (DrawingLock) {
            IReadOnlyList<ServiceHealth> latest = runContext.ServiceController.GetServiceHealth();

            if (!latest.SequenceEqual(samplers)) {
                samplers = latest;
                Resize();
                Clear();
                Draw();
                return;
            }

            if (statsView.Visible) {
                FillStats();
                statsView.Draw();
            }
        }
    }

    protected override void OnResize()
    {
        lock (DrawingLock) {
            runContext.Terminal.BackgroundColor = runContext.AppConfig.Theme.Background;

            int available   = Math.Max(0, Height - ReservedRows);
            bool robotFits  = Width >= mascot.MinimumWidth && available >= mascot.PreferredHeight;
            int robotHeight = robotFits ? mascot.PreferredHeight : Math.Min(mascot.CaptionHeight, available);

            (string Label, string Value)[] figures = EngineRows();

            int labelWidth  = figures.Max(row => row.Label.Length) + LabelGap;
            statsPairs      = Width >= 2 * (labelWidth + ValueWidth) ? 2 : 1;
            int statsLines  = figures.Length / statsPairs;
            int statsHeight = HeadingRows + statsLines;
            bool showStats  = robotFits && available >= robotHeight + SectionGap + statsHeight;

            int nameWidth  = samplers.Select(sampler => SamplerText(sampler).Length).DefaultIfEmpty(0).Max() + ColumnGap;
            int entryWidth = DotWidth + nameWidth;
            int listSpace  = available - robotHeight - SectionGap - statsHeight - SectionGap;
            int columns    = 0;
            int rows       = 0;

            if (samplers.Count > 0 && listSpace > 0) {
                int needed = (samplers.Count + listSpace - 1) / listSpace;

                columns = Math.Min(Width / entryWidth, Math.Max(Math.Min(2, samplers.Count), needed));
                rows    = columns > 0 ? (samplers.Count + columns - 1) / columns : 0;
            }

            bool showList = showStats && columns > 0 && rows <= listSpace;

            int block = robotHeight
                      + (showStats ? SectionGap + statsHeight : 0)
                      + (showList  ? SectionGap + rows        : 0);

            int top = Y + 1 + Math.Max(0, (available - block) / 2);

            mascot.CaptionOnly = !robotFits;
            mascot.X           = X;
            mascot.Y           = top;
            mascot.Width       = Width;
            mascot.Height      = robotHeight;

            headingY = top + robotHeight + SectionGap;

            int statsWidth = statsPairs * (labelWidth + ValueWidth);

            statsView.X       = X + Math.Max(0, (Width - statsWidth) / 2);
            statsView.Y       = headingY + HeadingRows;
            statsView.Width   = Math.Min(statsWidth, Width);
            statsView.Height  = statsLines;
            statsView.Visible = showStats;
            FillStats();

            int listWidth = columns * entryWidth;

            samplersView.X       = X + Math.Max(0, (Width - listWidth) / 2);
            samplersView.Y       = headingY + statsHeight + SectionGap;
            samplersView.Width   = listWidth;
            samplersView.Height  = rows;
            samplersView.Visible = showList;
            FillSamplers(columns, rows, nameWidth);

            base.OnResize();
        }
    }

    protected override void OnShown() => mascot.Play();

    private void OnSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        lock (DrawingLock) {
            samples       = e.Snapshot.Sequence;
            lastSampleUtc = e.Snapshot.TimestampUtc;
        }
    }

    protected override void OnUnload()
    {
        if (refreshTimer is { } timer) {
            using SysThreading::ManualResetEvent disposed = new(false);
            timer.Dispose(disposed);
            disposed.WaitOne();
            refreshTimer = null;
        }

        runContext.ServiceController.SystemSnapshotUpdated -= OnSnapshotUpdated;
        runContext.Terminal.CursorVisible = true;

        base.OnUnload();
    }

    private static DateTime ProcessStartUtc()
    {
        try {
            using Process process = Process.GetCurrentProcess();
            return process.StartTime.ToUniversalTime();
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or global::System.ComponentModel.Win32Exception) {
            return DateTime.UtcNow;
        }
    }

    private static string RunningAsText(bool elevated) =>
        !elevated                   ? Strings.AboutRunningAsStandardUser
      : OperatingSystem.IsWindows() ? Strings.AboutRunningAsAdministrator
                                    : Strings.AboutRunningAsRoot;

    private static string RuntimeText() =>
        string.Join(", ",
            RuntimeInformation.FrameworkDescription,
            RuntimeFeature.IsDynamicCodeSupported ? "JIT" : "NativeAOT",
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());

    private static string SamplerName(string name) =>
        Strings.ResourceManager.GetString($"ServiceName{name}", CultureInfo.CurrentUICulture) ?? name;

    private static string SamplerText(ServiceHealth sampler) =>
        sampler.Status == ServiceStatus.Running
            ? SamplerName(sampler.Name)
            : string.Format(CultureInfo.CurrentCulture, Strings.AboutSamplerStatus, SamplerName(sampler.Name), sampler.Status.ToDisplayText());

    private static string Title => Strings.AboutTitle;
}
