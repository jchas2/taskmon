using Task.Monitor.Configuration;
using Task.Monitor.Resources;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Metre;
using Task.Monitor.System.Services.Cpu;

namespace Task.Monitor.Gui.Controls.Cpu;

public sealed class CpuKernelUserMetreControl : Control
{
    private const int MetreRows  = 3;
    private const int LegendRows = 1;
    private const int BorderRows = 2;

    internal const int MetreHeight = MetreRows + LegendRows + BorderRows;

    private readonly AppConfig    appConfig;
    private readonly MetreControl metre;

    public CpuKernelUserMetreControl(ISystemTerminal terminal, AppConfig appConfig) : base(terminal)
    {
        this.appConfig = appConfig;

        metre = new MetreControl(terminal) {
            Border     = true,
            ShowLegend = true,
            Rows       = MetreRows,
            TabStop    = false,
            Text       = string.Empty
        };

        Controls.Add(metre);
    }

    internal MetreControl Metre => metre;

    protected override void OnDraw()
    {
        metre.Focused = Focused;
        metre.Draw();
    }

    protected override void OnLoad()
    {
        CommonControl.ThemeDefaults(this, appConfig.Theme);
        CommonControl.Theme(metre, appConfig.Theme);

        metre.MetreStyle = appConfig.MetreStyle;
        metre.AddSeries(Strings.CpuKernel, appConfig.Theme.PerformanceCpuKernel);
        metre.AddSeries(Strings.CpuUser,   appConfig.Theme.PerformanceCpuUser);

        base.OnLoad();
    }

    protected override void OnResize()
    {
        metre.X      = X;
        metre.Y      = Y;
        metre.Width  = Width;
        metre.Height = Height;

        base.OnResize();
    }

    protected override void OnUnload()
    {
        metre.ClearSeries();
        base.OnUnload();
    }

    public int RequiredHeight => metre.RequiredHeight;

    public void Sample(CpuMetrics metrics) =>
        metre.SetValues(metrics.CpuPercentKernelTime, metrics.CpuPercentUserTime);

    public override bool Visible
    {
        get => base.Visible;
        set {
            base.Visible = value;

            if (metre != null) {
                metre.Visible = value;
            }
        }
    }
}
