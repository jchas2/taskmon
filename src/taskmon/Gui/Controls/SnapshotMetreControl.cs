using System.Drawing;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls.Summary2.Layout;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Chart;
using Task.Monitor.System.Controls.Metre;
using Task.Monitor.System.Services;

namespace Task.Monitor.Gui.Controls;

public abstract class SnapshotMetreControl : Control, IFixedHeightPane
{
    private const double MidLoadThreshold  = 0.5;
    private const double HighLoadThreshold = 0.85;

    private readonly ServiceController serviceController;
    private readonly AppConfig         appConfig;
    private readonly MetreControl      metre;

    protected readonly record struct MetreReading(double[] Values, string ValueText);

    protected SnapshotMetreControl(
        ServiceController serviceController,
        ISystemTerminal   terminal,
        AppConfig         appConfig) : base(terminal)
    {
        this.serviceController = serviceController;
        this.appConfig         = appConfig;

        metre = new MetreControl(terminal) {
            Border       = true,
            MetreStyle   = MetreControlStyle.Dots,
            ShowLegend   = false,
            Rows         = 1,
            TabStop      = false,
            TextPosition = MetreTextPosition.TopLeft
        };

        Controls.Add(metre);
    }

    protected virtual void AddSeries(MetreControl metre, Theme theme) =>
        metre.AddSeries(Title, theme.RangeLowBackground);

    protected virtual bool ColourByLevel => true;

    public int FixedHeight => metre.RequiredHeight;

    protected static string FormatPercentage(double ratio) => MetreControl.FormatLegendPercentage(ratio);

    private Color LevelColour(double ratio) => ratio switch {
        >= HighLoadThreshold => appConfig.Theme.RangeHighBackground,
        >= MidLoadThreshold  => appConfig.Theme.RangeMidBackground,
                           _ => appConfig.Theme.RangeLowBackground
    };

    internal MetreControl Metre => metre;

    protected override void OnDraw()
    {
        if (Height < metre.RequiredHeight) {
            return;
        }

        metre.Focused = Focused;
        metre.Draw();

        if (Height > metre.RequiredHeight) {
            DrawRectangle(
                X,
                Y + metre.RequiredHeight,
                Width,
                Height - metre.RequiredHeight,
                BackgroundColour);
        }
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(metre, appConfig.Theme);

        metre.Text = Title;
        metre.ClearSeries();
        AddSeries(metre, appConfig.Theme);

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;
        base.OnLoad();
    }

    protected override void OnResize()
    {
        metre.X     = X;
        metre.Y     = Y;
        metre.Width = Width;

        base.OnResize();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) =>
        Sample(e.Snapshot);

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;
        metre.ClearSeries();

        base.OnUnload();
    }

    protected abstract MetreReading? Read(SystemSnapshot snapshot);

    public void Sample(SystemSnapshot snapshot)
    {
        if (Read(snapshot) is not { } reading) {
            return;
        }

        metre.Text = $"{Title} {reading.ValueText}";

        if (ColourByLevel && metre.Series is [{ } single] && reading.Values.Length > 0) {
            single.Colour = LevelColour(reading.Values[0]);
        }

        if (Height >= metre.RequiredHeight) {
            metre.SetValues(reading.Values);
        }
    }

    protected abstract string Title { get; }
}
