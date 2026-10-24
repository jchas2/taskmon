using System.Drawing;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Power;

namespace Task.Monitor.Gui.Controls.Thermals;

public sealed class PowerControl : Control
{
    public const int PreferredWidth = 56;

    private const int ComponentColumnWidth = 20;
    private const int RailColumnWidth      = 18;
    private const int PowerColumnWidth     = 10;
    private const int BorderAndGutters     = 3;

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly ListView          powerView;

    private SystemSnapshot? snapshot;

    public PowerControl(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        powerView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = false,
            Visible           = true,
            HeaderText        = "POWER",
            EmptyListViewText = "No power readings available."
        };

        powerView.ColumnHeaders
            .Add(new ListViewColumnHeader("COMPONENT"))
            .Add(new ListViewColumnHeader("RAIL"))
            .Add(new ListViewColumnHeader("POWER"));

        Controls.Add(powerView);
    }

    private string BuildComponentLabel(PowerReading reading) => reading.Component switch {
        PowerComponent.Cpu          => "CPU",
        PowerComponent.Gpu          => snapshot.ToGpuLabel(reading.ComponentId),
        PowerComponent.Disk         => snapshot.ToDiskLabel(reading.ComponentId),
        PowerComponent.Battery      => "Battery",
        PowerComponent.NeuralEngine => "Neural Engine",
        PowerComponent.Memory       => "Memory",
        PowerComponent.Display      => "Display",
                                  _ => "System"
    };

    private static int ComponentOrder(PowerComponent component) => component switch {
        PowerComponent.System       => 0,
        PowerComponent.Cpu          => 1,
        PowerComponent.Gpu          => 2,
        PowerComponent.NeuralEngine => 3,
        PowerComponent.Memory       => 4,
        PowerComponent.Display      => 5,
        PowerComponent.Disk         => 6,
                                  _ => 7
    };

    internal static (int Component, int Rail, int Power) ComputeColumnWidths(int width)
    {
        int available = Math.Max(0, width - BorderAndGutters);

        if (available >= ComponentColumnWidth + RailColumnWidth + PowerColumnWidth) {
            return (ComponentColumnWidth, RailColumnWidth, available - ComponentColumnWidth - RailColumnWidth);
        }

        int power     = Math.Min(PowerColumnWidth, available);
        int remaining = available - power;
        int component = remaining * ComponentColumnWidth / (ComponentColumnWidth + RailColumnWidth);

        return (component, remaining - component, power);
    }

    protected override void OnDraw()
    {
        powerView.Focused = Focused;
        powerView.Draw();
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(powerView, appConfig.Theme);

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
        base.OnLoad();
    }

    protected override void OnResize()
    {
        powerView.X      = X;
        powerView.Y      = Y;
        powerView.Width  = Width;
        powerView.Height = Height;

        (int component, int rail, int power) = ComputeColumnWidths(Width);

        powerView.ColumnHeaders[0].Width = component;
        powerView.ColumnHeaders[1].Width = rail;
        powerView.ColumnHeaders[2].Width = Math.Max(1, power);

        base.OnResize();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        powerView.Items.Clear();
        snapshot = null;

        base.OnUnload();
    }

    internal ListView PowerView => powerView;

    private void RebuildRows(PowerMetrics metrics)
    {
        powerView.Items.Clear();

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;

        foreach (PowerReading reading in metrics.Readings.OrderBy(reading => ComponentOrder(reading.Component))) {
            string watts = reading.IsRated
                ? $"{reading.Watts.ToWattText()} (rated)"
                : reading.Watts.ToWattText();

            powerView.Items.Add(new ListViewItem([BuildComponentLabel(reading), reading.Rail, watts], background, foreground));
        }
    }

    public void Sample(SystemSnapshot snapshot)
    {
        this.snapshot = snapshot;

        if (snapshot.Power is { } power) {
            RebuildRows(power.Metrics);
        }

        Draw();
    }
}
