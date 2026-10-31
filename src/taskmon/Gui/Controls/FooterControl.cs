using System.Drawing;
using System.Net;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Tips;
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
    private const string ErroredStatusText         = " Service status: Errored ";
    private const int    GlyphWidth                = 3;
    private const int    MissedCyclesBeforeUnknown = 3;
    private const int    RxTextWidth               = 11;
    private const string RunningStatusText         = " Services: Running ";
    private const char   StatusGlyph               = '●';
    private const string StoppingStatusText        = " Service status: Stopping ";
    private const string TipLabel                  = " Tip: ";
    private const int    TipRows                   = 2;
    private const string UnknownStatusText         = " Service status: Unknown ";

    private static readonly (string Key, string Label)[] KeyHints = [("F1", "Help"), ("F2", "Setup"), ("F10", "Exit")];

    public static readonly (string Key, string Label) NextTipHotKey = ("t", "Next Tip");

    internal static readonly TimeSpan TipRotationInterval = TimeSpan.FromMinutes(5);

    private static readonly int KeyHintsWidth   = KeyHints.Sum(hint =>
        CommonControl.FooterSeparator.Length + CommonControl.FormatHotKey(hint.Key).Length + 1 + hint.Label.Length);
    
    private static readonly int StatusTextWidth = new[] {
        ErroredStatusText, RunningStatusText, StoppingStatusText, UnknownStatusText }.Max(text => text.Length);

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

    // "● Tip: <tip>" with the next-tip hint right-aligned when the whole tip and the hint fit;
    // otherwise the hint is dropped and the tip cut short with an ellipsis.
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

    internal string CurrentTip => tips[tipIndex];

    private bool IsStale => missedCycles >= MissedCyclesBeforeUnknown;

    // Shows the next tip and restarts the rotation interval, so it isn't replaced moments later.
    public void NextTip()
    {
        lock (DrawingLock) {
            tipIndex   = (tipIndex + 1) % tips.Count;
            tipShownAt = UtcNow();
        }

        Draw();
    }

    protected override void OnDraw()
    {
        Color background = appConfig.Theme.Background;
        Color foreground = appConfig.Theme.Foreground;

        string banner = $" Task Monitor v{AssemblyVersionInfo.GetVersion()} ";

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
                ? $" {snapshot.Sequence} Rx".PadRight(RxTextWidth)
                : new string(' ', RxTextWidth);

            frame.SetColour(foreground, background);
            frame.Append(rxText);

            if (banner.Length + StatusTextWidth + GlyphWidth + rxText.Length + KeyHintsWidth <= Width) {
                frame.Append(CommonControl.FooterSeparator);
                frame.Append(CommonControl.FooterText(appConfig.Theme, KeyHints));
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

    internal int TipIndex
    {
        get => tipIndex;
        set => tipIndex = value;
    }

    internal Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;
}
