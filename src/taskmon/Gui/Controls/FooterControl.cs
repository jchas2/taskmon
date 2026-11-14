using System.Drawing;
using System.Globalization;
using System.Net;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Tips;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services;
using SysThreading = System.Threading;

namespace Task.Monitor.Gui.Controls;

public class FooterControl : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly AnsiScreenBuffer  frame = new();
    private readonly Lock              @lock = new();

    private readonly IReadOnlyList<string> tips = TipCatalogue.Tips;

    private SystemSnapshot?       snapshot = null;
    private SysThreading::Timer?  watchdogTimer;
    private int                   missedCycles;
    private bool                  loaded;
    private int                   tipIndex = Random.Shared.Next(TipCatalogue.Tips.Count);
    private DateTime?             tipShownAt;

    private const char   Ellipsis                  = '…';
    private const int    GlyphWidth                = 3;
    private const int    MissedCyclesBeforeUnknown = 3;
    private const int    RxTextWidth               = 11;
    private const char   StatusGlyph               = '●';
    private const int    TipRows                   = 2;

    internal static readonly TimeSpan TipRotationInterval = TimeSpan.FromMinutes(5);

    public FooterControl(
        ServiceController serviceController,
        ISystemTerminal   terminal,
        AppConfig         appConfig) : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;
    }

    private void AppendServiceStatus(SystemSnapshot current)
    {
        if (current.Services.Count == current.Services.Count(s => s.Status == ServiceStatus.Running)) {
            AppendStatus(Color.Green, RunningStatusText);
        }
        else if (current.Services.Any(s => s.Status == ServiceStatus.Errored)) {
            AppendStatus(Color.OrangeRed, ErroredStatusText);
        }
        else if (current.Services.Any(s => s.Status
                     is ServiceStatus.Stopping
                     or ServiceStatus.Stopped
                     or ServiceStatus.None)) {
            AppendStatus(Color.Orange, StoppingStatusText);
        }
        else {
            AppendStatus(Color.Orange, UnknownStatusText);
        }
    }

    private void AppendStatus(Color statusColour, string text)
    {
        frame.SetColour(Color.Black, statusColour);
        frame.Append(text.PadRight(StatusTextWidth));
    }

    private void AppendTipRow(Color foreground, Color background)
    {
        Theme theme       = appConfig.Theme;
        int   prefixWidth = 1 + TipLabel.Length;

        if (Width <= prefixWidth) {
            return;
        }

        RotateTipIfDue();

        string tip        = tips[tipIndex];
        int    tipWidth   = TipCatalogue.VisibleText(tip).Length;
        int    hintWidth  = CommonControl.FormatHotKey(NextTipHotKey.Key).Length + 1 + NextTipHotKey.Label.Length;
        bool   showHint   = prefixWidth + tipWidth + CommonControl.FooterSeparator.Length + hintWidth <= Width;
        int    tipBudget  = Width - prefixWidth - (showHint ? hintWidth : 0);

        frame.MoveTo(X, Y);
        frame.SetColour(theme.TipGlyph, background);
        frame.Append(StatusGlyph);
        frame.SetColour(foreground, background);
        frame.Append(TipLabel);

        int written = AppendTipText(tip, tipBudget, theme.HotKey, foreground, background);

        frame.SetColour(foreground, background);
        frame.Append(' ', tipBudget - written);

        if (showHint) {
            frame.Append(CommonControl.FooterText(theme, NextTipHotKey));
        }

        frame.ResetColour();
    }

    private int AppendTipText(string tip, int budget, Color keyColour, Color foreground, Color background)
    {
        bool truncate  = TipCatalogue.VisibleText(tip).Length > budget;
        int  remaining = truncate ? Math.Max(0, budget - 1) : budget;
        int  written   = 0;

        foreach ((string text, bool isKey) in TipCatalogue.Segments(tip)) {
            if (remaining == 0) {
                break;
            }

            string part = text.Length <= remaining ? text : text[..remaining];

            frame.SetColour(isKey ? keyColour : foreground, background);
            frame.Append(part);

            remaining -= part.Length;
            written   += part.Length;
        }

        if (truncate && budget > 0) {
            frame.SetColour(foreground, background);
            frame.Append(Ellipsis);
            written++;
        }

        return written;
    }

    private static (string Key, string Label)[] CoreKeyHints =>
        [("F1", Strings.FooterKeyHelp), ("F2", Strings.FooterKeySetup), ("F10", Strings.KeyBindExit)];

    private static int CoreKeyHintsWidth => HintsWidth(CoreKeyHints);

    internal string CurrentTip => tips[tipIndex];

    private static string ErroredStatusText  => $" {Strings.FooterServiceErrored} ";

    private static int HintsWidth((string Key, string Label)[] hints) =>
        hints.Sum(hint => CommonControl.FooterSeparator.Length + CommonControl.FormatHotKey(hint.Key).Length + 1 + hint.Label.Length);

    private bool IsStale => missedCycles >= MissedCyclesBeforeUnknown;

    private static (string Key, string Label)[] KeyHints =>
    [
        ("F1",  Strings.FooterKeyHelp),
        ("F2",  Strings.FooterKeySetup),
        ("F3",  Strings.FooterKeyTheme),
        ("F4",  Strings.FooterKeyLayout),
        ("F10", Strings.KeyBindExit)
    ];

    private static int KeyHintsWidth     => HintsWidth(KeyHints);

    public void NextTip()
    {
        lock (DrawingLock) {
            tipIndex   = (tipIndex + 1) % tips.Count;
            tipShownAt = UtcNow();
        }

        Draw();
    }

    public static (string Key, string Label) NextTipHotKey => ("t", Strings.FooterKeyNextTip);

    protected override void OnDraw()
    {
        Color background = appConfig.Theme.Background;
        Color foreground = appConfig.Theme.Foreground;

        string banner = $" {string.Format(CultureInfo.CurrentCulture, Strings.FooterBanner, AssemblyVersionInfo.GetVersion())} ";

        frame.Clear();

        if (Height >= TipRows) {
            AppendTipRow(foreground, background);
        }

        // The status row is always the bottom row, with the tip row above it when there is one.
        frame.MoveTo(X, Y + Height - 1);
        frame.SetColour(Color.Black, Color.DeepSkyBlue);
        frame.Append(banner);

        lock (@lock) {
            if (IsStale) {
                AppendStatus(Color.Orange, UnknownStatusText);
                frame.SetColour(Color.Orange, background);
                frame.Append($"  {StatusGlyph}");
            }
            else if (snapshot != null) {
                AppendServiceStatus(snapshot);

                if (snapshot.Sequence % 2 == 0) {
                    frame.SetColour(Color.DeepSkyBlue, background);
                    frame.Append($"  {StatusGlyph}");
                }
                else {
                    frame.SetColour(background, background);
                    frame.Append($"   ");
                }
            }
            else {
                frame.SetColour(background, background);
                frame.Append(' ', StatusTextWidth + GlyphWidth);
            }

            string rxText = snapshot != null
                ? $" {string.Format(CultureInfo.CurrentCulture, Strings.FooterReceived, snapshot.Sequence)}".PadRight(RxTextWidth)
                : new string(' ', RxTextWidth);

            frame.SetColour(foreground, background);
            frame.Append(rxText);

            int statusWidth = banner.Length + StatusTextWidth + GlyphWidth + rxText.Length;

            (string Key, string Label)[]? hints =
                statusWidth + KeyHintsWidth <= Width     ? KeyHints :
                statusWidth + CoreKeyHintsWidth <= Width ? CoreKeyHints :
                                                           null;

            if (hints != null) {
                frame.Append(CommonControl.FooterSeparator);
                frame.Append(CommonControl.FooterText(appConfig.Theme, hints));
                frame.ResetColour();
            }
        }

        Terminal.Write(frame.AsSpan());
    }

    protected override void OnLoad()
    {
        lock (@lock) {
            missedCycles = 0;
        }

        loaded        = true;
        watchdogTimer = new SysThreading::Timer(
            OnWatchdogTick, null, serviceController.Delay, SysThreading::Timeout.Infinite);

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
        base.OnLoad();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        lock (@lock) {
            snapshot     = e.Snapshot;
            missedCycles = 0;
        }

        Draw();
    }

    protected override void OnUnload()
    {
        loaded = false;

        if (watchdogTimer is { } timer) {
            using SysThreading::ManualResetEvent disposed = new(false);
            timer.Dispose(disposed);
            disposed.WaitOne();
            watchdogTimer = null;
        }

        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        base.OnUnload();
    }

    internal void OnWatchdogTick(object? state)
    {
        if (!loaded) {
            return;
        }

        bool becameStale = false;

        lock (@lock) {
            if (missedCycles < MissedCyclesBeforeUnknown) {
                missedCycles++;
                becameStale = IsStale;
            }
        }

        try {
            watchdogTimer?.Change(serviceController.Delay, SysThreading::Timeout.Infinite);
        }
        catch (ObjectDisposedException) {
            return;
        }

        if (becameStale) {
            Draw();
        }
    }

    // Called while drawing, under DrawingLock: the footer redraws on every snapshot, so this
    // needs no timer of its own.
    private void RotateTipIfDue()
    {
        DateTime now = UtcNow();

        if (tipShownAt is not { } shownAt) {
            tipShownAt = now;
            return;
        }

        if (now - shownAt >= TipRotationInterval) {
            tipIndex   = (tipIndex + 1) % tips.Count;
            tipShownAt = now;
        }
    }

    // The footer's height: a tip row above the status row when tips are on.
    public int Rows => appConfig.ShowTips ? TipRows : 1;

    private static string RunningStatusText  => $" {Strings.FooterServicesRunning} ";

    private static int StatusTextWidth => new[] {
        ErroredStatusText, RunningStatusText, StoppingStatusText, UnknownStatusText }.Max(text => text.Length);

    private static string StoppingStatusText => $" {Strings.FooterServiceStopping} ";

    internal int TipIndex
    {
        get => tipIndex;
        set => tipIndex = value;
    }

    private static string TipLabel           => $" {Strings.FooterTip} ";
    private static string UnknownStatusText  => $" {Strings.FooterServiceUnknown} ";

    internal Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;
}
