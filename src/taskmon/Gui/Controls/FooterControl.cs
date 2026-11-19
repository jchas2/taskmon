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
using Task.Monitor.System.Services.Health;
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

    private void AppendHealthStatus(HealthLevel level)
    {
        Theme theme = appConfig.Theme;

        (Color foreground, Color background) = level switch {
            HealthLevel.Ok       => (theme.StatusbarHealthOkForeground,       theme.StatusbarHealthOkBackground),
            HealthLevel.Warning  => (theme.StatusbarHealthWarningForeground,  theme.StatusbarHealthWarningBackground),
            HealthLevel.Critical => (theme.StatusbarHealthCriticalForeground, theme.StatusbarHealthCriticalBackground),
                               _ => (theme.StatusbarHealthUnknownForeground,  theme.StatusbarHealthUnknownBackground)
        };

        frame.SetColour(foreground, background);
        frame.Append(HealthStatusText(level).PadRight(StatusTextWidth));
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

    private static string HealthStatusText(HealthLevel level) =>
        $" {string.Format(CultureInfo.CurrentCulture, Strings.FooterHealth, level.ToDisplayText())} ";

    private static int HintsWidth((string Key, string Label)[] hints) =>
        hints.Sum(hint => CommonControl.FooterSeparator.Length + CommonControl.FormatHotKey(hint.Key).Length + 1 + hint.Label.Length);

    private bool IsStale => missedCycles >= MissedCyclesBeforeUnknown;

    private static (string Key, string Label)[] KeyHints =>
    [
        ("F1",  Strings.FooterKeyHelp),
        ("F2",  Strings.FooterKeySetup),
        ("F3",  Strings.FooterKeyTheme),
        ("F4",  Strings.FooterKeyLayout),
        ("F10", Strings.KeyBindExit),
        ("F12", Strings.FooterKeyAbout)
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

        string banner = $" {string.Format(CultureInfo.CurrentCulture, Strings.FooterBanner, AssemblyVersionInfo.GetShortVersion())} ";

        frame.Clear();

        if (Height >= TipRows) {
            AppendTipRow(foreground, background);
        }

        frame.MoveTo(X, Y + Height - 1);
        frame.SetColour(appConfig.Theme.StatusbarBannerForeground, appConfig.Theme.StatusbarBannerBackground);
        frame.Append(banner);

        lock (@lock) {
            if (IsStale) {
                AppendHealthStatus(HealthLevel.Unknown);
                frame.SetColour(appConfig.Theme.StatusbarStatusWarning, background);
                frame.Append($"  {StatusGlyph}");
            }
            else if (snapshot != null) {
                AppendHealthStatus(snapshot.Health?.Level ?? HealthLevel.Unknown);

                if (snapshot.Sequence % 2 == 0) {
                    frame.SetColour(appConfig.Theme.StatusbarPulse, background);
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

    public int Rows => appConfig.ShowTips ? TipRows : 1;

    private static int StatusTextWidth =>
        Enum.GetValues<HealthLevel>().Max(level => HealthStatusText(level).Length);

    internal int TipIndex
    {
        get => tipIndex;
        set => tipIndex = value;
    }

    private static string TipLabel => $" {Strings.FooterTip} ";

    internal Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;
}
