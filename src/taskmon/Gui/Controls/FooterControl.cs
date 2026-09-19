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
    private SystemSnapshot? snapshot = null;
    private readonly Lock @lock = new();

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

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e)
    {
        lock (@lock) {
            snapshot = e.Snapshot;
        }

        Draw();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        base.OnUnload();
    }

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
            if (snapshot != null) {
                if (snapshot.Services.Count == snapshot.Services.Count(s => s.Status == ServiceStatus.Running)) {
                    frame.SetColour(Color.Black, Color.Green);
                    frame.Append(" Services: Running ");
                }
                else if (snapshot.Services.Any(s => s.Status == ServiceStatus.Errored)) {
                    frame.SetColour(Color.Black, Color.OrangeRed);
                    frame.Append(" Service status: Errored ");
                }
                else if (snapshot.Services.Any(s => s.Status 
                             is ServiceStatus.Stopping
                             or ServiceStatus.Stopped
                             or ServiceStatus.None)) {
                    frame.SetColour(Color.Black, Color.Orange);
                    frame.Append(" Service status: Stopping ");
                }
                else {
                    frame.SetColour(Color.Black, Color.Orange);
                    frame.Append(" Service status: Unknown ");
                }

                //Color ledColour = snapshot.Sequence % 2 == 0 ? Color.DeepSkyBlue : background;

                if (snapshot.Sequence % 2 == 0) {
                    frame.SetColour(Color.DeepSkyBlue, background);
                    frame.Append($"  {StatusGlyph}");
                }
                else {
                    frame.SetColour(background, background);
                    frame.Append($"   ");
                }
                
                frame.SetColour(foreground, background);
                frame.Append($" {snapshot.Sequence} Rx");
            }
        }

        Terminal.Write(frame.AsSpan());
    }
}
