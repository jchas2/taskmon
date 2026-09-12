using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls;

public class FooterControl : Control
{
    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private readonly AnsiScreenBuffer frame = new();

    // The latest service-health list off the snapshot. Each entry redraws the footer only when the
    // list actually changes - a service transition is rare next to the publish rate.
    private IReadOnlyList<ServiceHealth> services = [];

    private const char StatusGlyph = '●';

    public FooterControl(
        ServiceController serviceController,
        ISystemTerminal terminal,
        AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig = appConfig;
    }

    protected override void OnLoad()
    {
        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
        base.OnLoad();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    // Wired to the controller event in OnLoad; tests call it directly. The footer redraws only when
    // the health list actually changes - a service transition is rare next to the publish rate.
    public void Sample(SystemSnapshot snapshot)
    {
        if (services.SequenceEqual(snapshot.Services)) {
            return;
        }

        services = snapshot.Services;
        Draw();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        base.OnUnload();
    }

    protected override void OnDraw()
    {
        try {
            Control.DrawingLockAcquire();
            OnDrawInternal();
        }
        finally {
            Control.DrawingLockRelease();
        }
    }

    private void OnDrawInternal()
    {
        Color background = appConfig.DefaultTheme.Background;
        Color foreground = appConfig.DefaultTheme.Foreground;

        string banner = $" Task Monitor v{AssemblyVersionInfo.GetVersion()} ";

        frame.Clear();
        frame.MoveTo(X, Y);
        frame.SetColour(Color.Black, Color.DeepSkyBlue);
        frame.Append(banner);
        frame.SetColour(foreground, background);
        
        // Each health token is "<glyph> <name>  ". Drop the cluster when the row is too narrow for
        // the banner plus all of it rather than wrapping onto the line above.
        int clusterWidth = services.Sum(service => TokenWidth(service.Name));

        if (services.Count > 0 && banner.Length + clusterWidth <= Width) {
            frame.Append(' ', Width - banner.Length - clusterWidth);

            foreach (ServiceHealth service in services) {
                frame.SetColour(StatusColour(service.Status), background);
                frame.Append(StatusGlyph);
                frame.SetColour(foreground, background);
                frame.Append($" {service.Name}  ");
            }
        }
        else {
            frame.Append(' ', Math.Max(0, Width - banner.Length));
        }

        Terminal.Write(frame.AsSpan());
    }

    private static int TokenWidth(string name) => name.Length + 4;

    private Color StatusColour(ServiceStatus status) => status switch {
        ServiceStatus.Running => Color.Green,
        ServiceStatus.Starting or ServiceStatus.Stopping => Color.Orange,
        ServiceStatus.Errored => Color.Red,
        _ => appConfig.DefaultTheme.Foreground
    };
}
