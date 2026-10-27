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

    private const int    MissedCyclesBeforeUnknown = 3;
    private const char   StatusGlyph               = '●';
    private const string UnknownStatusText         = " Service status: Unknown ";

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
            frame.SetColour(Color.Black, Color.Green);
            frame.Append(" Services: Running ");
        }
        else if (current.Services.Any(s => s.Status == ServiceStatus.Errored)) {
            frame.SetColour(Color.Black, Color.OrangeRed);
            frame.Append(" Service status: Errored ");
        }
        else if (current.Services.Any(s => s.Status
                     is ServiceStatus.Stopping
                     or ServiceStatus.Stopped
                     or ServiceStatus.None)) {
            frame.SetColour(Color.Black, Color.Orange);
            frame.Append(" Service status: Stopping ");
        }
        else {
            frame.SetColour(Color.Black, Color.Orange);
            frame.Append(UnknownStatusText);
        }
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
                frame.SetColour(Color.Black, Color.Orange);
                frame.Append(UnknownStatusText);
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

            if (snapshot != null) {
                frame.SetColour(foreground, background);
                frame.Append($" {snapshot.Sequence} Rx");
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
