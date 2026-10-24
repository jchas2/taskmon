using System.Drawing;
using Task.Monitor.Configuration;
using Task.Monitor.Extensions;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.ListView;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.Gui.Controls.Thermals;

public sealed class FansControl : Control
{
    private const int BorderAndHeader  = 3;
    private const int BorderAndGutters = 3;
    private const int ColumnCount      = 5;
    private const int ColumnWidth      = 10;

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly ListView          fansView;

    public FansControl(ServiceController serviceController, ISystemTerminal terminal, AppConfig appConfig)
        : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        fansView = new ListView(terminal) {
            EnableScroll      = false,
            EnableRowSelect   = false,
            ShowColumnHeaders = true,
            ShowBorder        = true,
            TabStop           = false,
            Visible           = true,
            HeaderText        = "FANS",
            EmptyListViewText = "No fans."
        };

        fansView.ColumnHeaders
            .Add(new ListViewColumnHeader("FAN"))
            .Add(new ListViewColumnHeader("RPM"))
            .Add(new ListViewColumnHeader("MIN"))
            .Add(new ListViewColumnHeader("MAX"))
            .Add(new ListViewColumnHeader("LOAD"));

        Controls.Add(fansView);
    }

    internal static int[] ComputeColumnWidths(int width)
    {
        int available = Math.Max(0, width - BorderAndGutters);
        int fixedWidth = ColumnWidth * (ColumnCount - 1);

        if (available > fixedWidth) {
            return [ColumnWidth, ColumnWidth, ColumnWidth, ColumnWidth, available - fixedWidth];
        }

        int[] widths = Enumerable.Repeat(available / ColumnCount, ColumnCount).ToArray();
        widths[^1] += available % ColumnCount;

        return widths;
    }

    internal ListView FansView => fansView;

    public static int HeightFor(int fanCount) => BorderAndHeader + Math.Max(1, fanCount);

    protected override void OnDraw()
    {
        fansView.Focused = Focused;
        fansView.Draw();
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(fansView, appConfig.Theme);

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
        base.OnLoad();
    }

    protected override void OnResize()
    {
        fansView.X      = X;
        fansView.Y      = Y;
        fansView.Width  = Width;
        fansView.Height = Height;

        int[] widths = ComputeColumnWidths(Width);

        for (int i = 0; i < widths.Length; i++) {
            fansView.ColumnHeaders[i].Width = Math.Max(1, widths[i]);
        }

        base.OnResize();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        fansView.Items.Clear();

        base.OnUnload();
    }

    public void Sample(SystemSnapshot snapshot)
    {
        if (snapshot.Thermal is not { } thermal) {
            return;
        }

        fansView.Items.Clear();

        Color background = appConfig.Theme.ListViewBackground;
        Color foreground = appConfig.Theme.ListViewForeground;

        foreach (FanReading fan in thermal.Metrics.Fans) {
            fansView.Items.Add(new ListViewItem(
                [fan.Name, fan.ToSpeedText(), fan.MinRpm.ToRpmText(), fan.MaxRpm.ToRpmText(), fan.ToLoadText()],
                background,
                foreground));
        }

        Draw();
    }
}
