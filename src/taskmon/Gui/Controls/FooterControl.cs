using System.Drawing;
using System.Net;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
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

    private SystemSnapshot?       snapshot = null;
    private SysThreading::Timer?  watchdogTimer;
    private int                   missedCycles;
    private bool                  loaded;

    private const string ErroredStatusText         = " Service status: Errored ";
    private const int    GlyphWidth                = 3;
    private const int    MissedCyclesBeforeUnknown = 3;
    private const int    RxTextWidth               = 11;
    private const string RunningStatusText         = " Services: Running ";
    private const char   StatusGlyph               = '●';
    private const string StoppingStatusText        = " Service status: Stopping ";
    private const string UnknownStatusText         = " Service status: Unknown ";

    private static readonly (string Key, string Label)[] KeyHints = [("F1", "Help"), ("F2", "Setup"), ("F10", "Exit")];

    private static readonly int KeyHintsWidth   = KeyHints.Sum(hint => 2 + hint.Key.Length + 1 + hint.Label.Length);
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

    private void AppendKeyHints(Color foreground, Color background)
    {
        foreach ((string key, string label) in KeyHints) {
            frame.SetColour(Color.DeepSkyBlue, background);
            frame.Append($"  {key} ");
            frame.SetColour(foreground, background);
            frame.Append(label);
        }
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

    private bool IsStale => missedCycles >= MissedCyclesBeforeUnknown;

    protected override void OnDraw()
    {
        Color background = appConfig.Theme.Background;
        Color foreground = appConfig.Theme.Foreground;

        string banner = $" Task Monitor v{AssemblyVersionInfo.GetVersion()} ";

        frame.Clear();
        frame.MoveTo(X, Y);
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

                //Color ledColour = snapshot.Sequence % 2 == 0 ? Color.DeepSkyBlue : background;

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
                AppendKeyHints(foreground, background);
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
}
