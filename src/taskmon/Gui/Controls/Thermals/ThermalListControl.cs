using System.Drawing;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.Gui.Controls.Thermals;

public sealed class ThermalListControl : Control
{
    private const int ComponentColumnWidth   = 15;
    private const int TemperatureColumnWidth = 7;
    private const int BorderAndGutters       = 3;

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly ListView          thermalView;

    public ThermalListControl(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        thermalView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = false,
            Visible           = true,
            HeaderText        = Strings.ThermalListHeader,
            EmptyListViewText = Strings.ThermalsNoSensors
        };

        thermalView.ColumnHeaders
            .Add(new ListViewColumnHeader(Strings.ThermalListColumnComponent))
            .Add(new ListViewColumnHeader(Strings.ThermalListColumnName))
            .Add(new ListViewColumnHeader(Strings.ThermalListColumnTemperature));

        Controls.Add(thermalView);
    }

    internal static (int Component, int Name, int Temperature) ComputeColumnWidths(int width)
    {
        int available   = Math.Max(0, width - BorderAndGutters);
        int temperature = Math.Min(TemperatureColumnWidth, available);
        int component   = Math.Min(ComponentColumnWidth, available - temperature);

        return (component, available - temperature - component, temperature);
    }

    protected override void OnDraw()
    {
        thermalView.Focused = Focused;
        thermalView.Draw();
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(thermalView, appConfig.Theme);

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
        base.OnLoad();
    }

    protected override void OnResize()
    {
        thermalView.X      = X;
        thermalView.Y      = Y;
        thermalView.Width  = Width;
        thermalView.Height = Height;

        (int component, int name, int temperature) = ComputeColumnWidths(Width);

        thermalView.ColumnHeaders[0].Width = component;
        thermalView.ColumnHeaders[1].Width = name;
        thermalView.ColumnHeaders[2].Width = Math.Max(1, temperature);

        base.OnResize();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        thermalView.Items.Clear();

        base.OnUnload();
    }

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.Thermal is not { } thermal) {
            return;
        }

        thermalView.Items.Clear();

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;

        List<(ThermalComponent Component, string ComponentId)> components = thermal.Metrics.Sensors
            .Select(sensor => (sensor.Component, sensor.ComponentId))
            .Distinct()
            .OrderBy(group => group.Component)
            .ThenBy(group  => group.ComponentId, StringComparer.Ordinal)
            .ToList();

        foreach ((ThermalComponent component, string componentId) in components) {
            string name = component switch {
                ThermalComponent.Cpu  => snapshot.Cpu?.Specs.CpuName ?? string.Empty,
                ThermalComponent.Gpu  => snapshot.ToGpuName(componentId),
                ThermalComponent.Disk => snapshot.ToDiskName(componentId),
                                    _ => string.Empty
            };

            thermalView.Items.Add(new ListViewItem(
                [
                    component.ToComponentText(componentId),
                    name,
                    thermal.Metrics.PrimaryTemperature(component, componentId).ToTemperatureText()
                ],
                background,
                foreground));
        }

        Draw();
    }

    internal ListView ThermalView => thermalView;
}
