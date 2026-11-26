using System.Drawing;
using System.Globalization;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Power;

namespace Task.Monitor.Gui.Controls.Thermals;

public sealed class PowerControl : Control
{
    public const int PreferredWidth = 72;

    private const int    BorderAndGutters = 3;
    private const int    ColumnSpacing    = 1;
    private const double WidestWatts      = 888.8;

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly ListView          powerView;

    private SystemSnapshot? snapshot;

    private int componentWidth;
    private int railWidth;
    private int powerWidth;

    public PowerControl(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        powerView = new ListView(terminal) {
            EnableScroll      = true,
            EnableRowSelect   = true,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = false,
            Visible           = true,
            HeaderText        = Strings.PowerHeader,
            EmptyListViewText = Strings.PowerNone
        };

        powerView.ColumnHeaders
            .Add(new ListViewColumnHeader(Strings.PowerColumnComponent))
            .Add(new ListViewColumnHeader(Strings.PowerColumnName))
            .Add(new ListViewColumnHeader(Strings.PowerColumnRail))
            .Add(new ListViewColumnHeader(Strings.PowerColumnPower));

        Controls.Add(powerView);
    }

    private string BuildComponentLabel(PowerReading reading) => reading.Component switch {
        PowerComponent.Cpu          => Strings.PowerCpu,
        PowerComponent.Gpu          => Strings.SnapshotGpu,
        PowerComponent.Disk         => string.Format(CultureInfo.CurrentCulture, Strings.SnapshotDiskLabelShort, reading.ComponentId),
        PowerComponent.Battery      => Strings.PowerBattery,
        PowerComponent.NeuralEngine => Strings.PowerNeuralEngine,
        PowerComponent.Memory       => Strings.PowerMemory,
        PowerComponent.Display      => Strings.PowerDisplay,
                                  _ => Strings.PowerSystem
    };

    private string BuildName(PowerReading reading) => reading.Component switch {
        PowerComponent.Cpu  => snapshot?.Cpu?.Specs.CpuName ?? string.Empty,
        PowerComponent.Gpu  => snapshot.ToGpuName(reading.ComponentId),
        PowerComponent.Disk => snapshot.ToDiskName(reading.ComponentId),
                          _ => string.Empty
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

    internal static (int Component, int Name, int Rail, int Power) ComputeColumnWidths(
        int width, 
        int component, 
        int rail, 
        int power)
    {
        int available = Math.Max(0, width - BorderAndGutters);

        power     = Math.Min(power,     available);
        component = Math.Min(component, available - power);
        rail      = Math.Min(rail,      available - power - component);

        return (component, available - power - component - rail, rail, power);
    }

    private void LayoutColumns()
    {
        (int component, int name, int rail, int power) = ComputeColumnWidths(
            Width,
            ColumnSpacing + 1 + Math.Max(componentWidth, Strings.PowerColumnComponent.TerminalWidth()),
            ColumnSpacing + 1 + Math.Max(railWidth,      Strings.PowerColumnRail.TerminalWidth()),
                            1 + Math.Max(powerWidth,     Strings.PowerColumnPower.TerminalWidth()));

        powerView.ColumnHeaders[0].Width = component;
        powerView.ColumnHeaders[1].Width = name;
        powerView.ColumnHeaders[2].Width = rail;
        powerView.ColumnHeaders[3].Width = Math.Max(1, power);
    }

    protected override void OnDraw()
    {
        powerView.Focused = Focused;
        powerView.Draw();
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled) =>
        powerView.KeyPressed(keyInfo, ref handled);

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

        LayoutColumns();

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
        int top      = powerView.TopIndex;
        int selected = powerView.SelectedIndex;

        powerView.Items.Clear();

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;

        string Rated(string watts) => string.Format(CultureInfo.CurrentCulture, Strings.PowerRated, watts);

        componentWidth = 0;
        railWidth      = 0;
        powerWidth     = 0;

        foreach (PowerReading reading in metrics.Readings.OrderBy(reading => ComponentOrder(reading.Component))) {
            string component = BuildComponentLabel(reading);
            
            string watts = reading.IsRated 
                ? Rated(reading.Watts.ToWattText()) 
                : reading.Watts.ToWattText();

            string widestWatts = reading.IsRated 
                ? Rated(WidestWatts.ToWattText()) 
                : WidestWatts.ToWattText();

            componentWidth = Math.Max(componentWidth, component.TerminalWidth());
            railWidth      = Math.Max(railWidth,      reading.Rail.TerminalWidth());
            powerWidth     = Math.Max(powerWidth,     widestWatts.TerminalWidth());

            powerView.Items.Add(new ListViewItem([component, BuildName(reading), reading.Rail, watts], background, foreground));
        }

        if (powerView.Items.Count > 0) {
            powerView.TopIndex      = top;
            powerView.SelectedIndex = Math.Min(selected, powerView.Items.Count - 1);
            powerView.ScrollSelectedIntoView();
        }
    }

    public void Sample(SystemSnapshot snapshot)
    {
        this.snapshot = snapshot;

        if (snapshot.Power is { } power) {
            RebuildRows(power.Metrics);
            LayoutColumns();
        }

        Draw();
    }
}
