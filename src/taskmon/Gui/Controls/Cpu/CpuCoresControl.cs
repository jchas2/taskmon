using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.Metre;
using Task.Monitor.System.Services;
using Task.Monitor.System.Services.Cpu;

namespace Task.Monitor.Gui.Controls.Cpu;

// One horizontal metre per logical processor, the way htop/btop present them, stacked into as
// many columns as the pane's width allows. Only ever reached by assigning PaneControlType.CpuCores
// to a pane in LayoutDesignerScreen - it is on no menu or screen of its own.
//
// Feeds itself from the snapshot stream (like the Process/Drivers/Services panes, unlike the Chart
// panes SummaryChartFeeder pushes into), so it shows live data in the designer preview and on a
// SummaryControl2 dashboard without either of them knowing anything about it.
public sealed class CpuCoresControl : Control
{
    private const string Title = "CPU CORES";
    private const string MsgNoCoreData = "Per-core CPU data is not available on this OS";

    // Widths of a single core's row: "C0 <bar> 100%". The bar gets whatever the column has left
    // over, but never less than MinBarWidth - below that the row reads as noise, so a column that
    // narrow isn't drawn at all.
    private const int MinBarWidth = 6;
    private const int PercentWidth = 4;
    private const int ColumnGutter = 2;

    // Load bands for the bar colour, matching the performance panels' low/mid/high gradient.
    private const double MidLoadThreshold = 0.5;
    private const double HighLoadThreshold = 0.85;

    private readonly ServiceController serviceController;
    private readonly AppConfig appConfig;
    private readonly AnsiScreenBuffer frame = new();

    // One metre per drawn core, rebuilt only when the grid shape or the core count changes - not
    // per frame, which would churn a metre's sub-cell buffers on every tick.
    private readonly List<MetreControl> metres = [];
    private string metreSignature = string.Empty;

    private string[] coreNames = [];
    private double[] coreValues = [];

    // A shrinking core count (or a resize) leaves rows behind that nothing would otherwise
    // overwrite, so the content area is wiped once when the shape changes.
    private bool needsClear = true;

    public CpuCoresControl(
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
        BackgroundColour = appConfig.Theme.Background;
        ForegroundColour = appConfig.Theme.Foreground;

        serviceController.SystemSnapshotUpdated += OnSystemSnapshotUpdated;

        base.OnLoad();
    }

    protected override void OnUnload()
    {
        serviceController.SystemSnapshotUpdated -= OnSystemSnapshotUpdated;

        metres.Clear();
        metreSignature = string.Empty;

        base.OnUnload();
    }

    protected override void OnResize()
    {
        needsClear = true;
        base.OnResize();
    }

    private void OnSystemSnapshotUpdated(object? sender, SystemSnapshotEventArgs e) => Sample(e.Snapshot);

    // Wired to the controller event in OnLoad; tests call it directly.
    public void Sample(SystemSnapshot snapshot)
    {
        try {
            DrawingLockAcquire();

            if (snapshot.Cpu is { } cpu && cpu.CoreMetrics.Length > 0) {
                if (coreValues.Length != cpu.CoreMetrics.Length) {
                    coreNames = new string[cpu.CoreMetrics.Length];
                    coreValues = new double[cpu.CoreMetrics.Length];
                    needsClear = true;
                }

                for (int i = 0; i < cpu.CoreMetrics.Length; i++) {
                    CpuInfo.CpuCoreMetric core = cpu.CoreMetrics[i];
                    coreNames[i] = string.IsNullOrWhiteSpace(core.Name) ? i.ToString() : core.Name;
                    coreValues[i] = Math.Clamp(core.Value, 0.0, 1.0);
                }
            }
        }
        finally {
            DrawingLockRelease();
        }

        Draw();
    }

    protected override void OnDraw()
    {
        if (Width < 2 || Height < 2) {
            return;
        }

        DrawBorder();

        int innerLeft = X + 1;
        int innerTop = Y + 1;
        int innerWidth = Width - 2;
        int innerHeight = Height - 2;

        if (innerWidth <= 0 || innerHeight <= 0) {
            return;
        }

        if (needsClear) {
            DrawRectangle(innerLeft, innerTop, innerWidth, innerHeight, BackgroundColour);
            needsClear = false;
        }

        if (coreValues.Length == 0) {
            DrawCentredMessage(innerLeft, innerTop, innerWidth, innerHeight, MsgNoCoreData);
            return;
        }

        CoreGrid grid = CalculateGrid(coreValues.Length, innerWidth, innerHeight);

        if (grid.Columns == 0) {
            return;
        }

        EnsureMetres(grid);

        int metreIndex = 0;

        for (int column = 0; column < grid.Columns; column++) {
            int columnLeft = innerLeft + column * (grid.ColumnWidth + ColumnGutter);

            for (int row = 0; row < grid.Rows; row++) {
                // Column-major: the first column holds the first grid.Rows cores, and so on, so a
                // 5-high pane showing 8 cores reads C0-C4 then C5-C7.
                int coreIndex = column * grid.Rows + row;

                if (coreIndex >= coreValues.Length) {
                    break;
                }

                DrawCore(metres[metreIndex++], coreIndex, columnLeft, innerTop + row, grid);
            }
        }
    }

    private void DrawCore(MetreControl metre, int coreIndex, int left, int top, CoreGrid grid)
    {
        double value = coreValues[coreIndex];
        string label = $"C{coreNames[coreIndex]}";

        frame.Clear();
        frame.MoveTo(left, top);
        frame.SetColour(ForegroundColour, BackgroundColour);
        frame.Append(label.Length > grid.LabelWidth ? label[..grid.LabelWidth] : label.PadRight(grid.LabelWidth));
        frame.Append(' ');
        frame.ResetColour();
        Terminal.Write(frame.AsSpan());

        metre.X = left + grid.LabelWidth + 1;
        metre.Y = top;
        metre.Width = grid.BarWidth;
        metre.MetreStyle = appConfig.MetreStyle;
        metre.BackgroundColour = BackgroundColour;
        metre.ForegroundColour = ForegroundColour;
        metre.Series[0].Colour = LoadColour(value);
        metre.SetValue(0, value);

        frame.Clear();
        frame.MoveTo(metre.X + grid.BarWidth, top);
        frame.SetColour(ForegroundColour, BackgroundColour);
        frame.Append($"{value * 100:0}%".PadLeft(PercentWidth));
        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }

    private Color LoadColour(double value) => value switch {
        >= HighLoadThreshold => appConfig.Theme.RangeHighBackground,
        >= MidLoadThreshold => appConfig.Theme.RangeMidBackground,
        _ => appConfig.Theme.RangeLowBackground
    };

    // The metres are interchangeable - each one is positioned and given its value at draw time -
    // so only how many of them exist has to be kept in step with the grid.
    private void EnsureMetres(CoreGrid grid)
    {
        int required = Math.Min(coreValues.Length, grid.Columns * grid.Rows);
        string signature = $"{required}:{grid.BarWidth}";

        if (signature == metreSignature && metres.Count == required) {
            return;
        }

        metreSignature = signature;
        metres.Clear();

        for (int i = 0; i < required; i++) {
            MetreControl metre = new(Terminal) {
                ShowLegend = false,
                Rows = 1,
            };

            metre.AddSeries(string.Empty, appConfig.Theme.RangeLowBackground);
            metres.Add(metre);
        }
    }

    // Column-major packing: every column is filled top to bottom before the next one starts, so the
    // number of columns follows from the height. Columns that wouldn't leave room for a readable
    // bar are dropped, and the cores that would have been in them simply aren't drawn.
    internal static CoreGrid CalculateGrid(int coreCount, int innerWidth, int innerHeight)
    {
        if (coreCount <= 0 || innerWidth <= 0 || innerHeight <= 0) {
            return new CoreGrid(0, 0, 0, 0, 0);
        }

        int labelWidth = 1 + Math.Max(1, coreCount - 1).ToString().Length;
        int minColumnWidth = labelWidth + 1 + MinBarWidth + PercentWidth;

        if (innerWidth < minColumnWidth) {
            return new CoreGrid(0, 0, 0, 0, 0);
        }

        int rows = Math.Min(innerHeight, coreCount);
        int wanted = (coreCount + rows - 1) / rows;
        int affordable = (innerWidth + ColumnGutter) / (minColumnWidth + ColumnGutter);
        int columns = Math.Max(1, Math.Min(wanted, affordable));

        int columnWidth = (innerWidth - (columns - 1) * ColumnGutter) / columns;
        int barWidth = columnWidth - labelWidth - 1 - PercentWidth;

        return new CoreGrid(columns, rows, columnWidth, labelWidth, barWidth);
    }

    internal readonly record struct CoreGrid(
        int Columns,
        int Rows,
        int ColumnWidth,
        int LabelWidth,
        int BarWidth);

    private void DrawCentredMessage(int left, int top, int width, int height, string message)
    {
        string shown = message.Length > width ? message[..width] : message;
        int messageLeft = left + (width - shown.Length) / 2;

        frame.Clear();
        frame.MoveTo(messageLeft, top + height / 2);
        frame.SetColour(ForegroundColour, BackgroundColour);
        frame.Append(shown);
        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }

    // Mirrors ListView.DrawBorder - rounded corners, the title centred in the top border - so the
    // pane reads as one more bordered panel, and the designer's selection highlight (a BorderColour
    // swap) lands on it the same way it does on a Chart or a ListView pane.
    private void DrawBorder()
    {
        int innerWidth = Width - 2;
        string titleLabel = $" {Title} ";
        int titleLength = Math.Min(titleLabel.Length, innerWidth);
        int leftDashes = (innerWidth - titleLength) / 2;
        int rightDashes = innerWidth - titleLength - leftDashes;

        frame.Clear();
        frame.MoveTo(X, Y);
        frame.SetColour(BorderColour, BackgroundColour);
        frame.Append('╭');
        frame.Append('─', leftDashes);
        frame.SetColour(ForegroundColour, BackgroundColour);
        frame.Append(titleLabel.AsSpan(0, titleLength));
        frame.SetColour(BorderColour, BackgroundColour);
        frame.Append('─', rightDashes);
        frame.Append('╮');

        for (int row = 1; row < Height - 1; row++) {
            frame.MoveTo(X, Y + row);
            frame.SetColour(BorderColour, BackgroundColour);
            frame.Append('│');

            frame.MoveTo(X + Width - 1, Y + row);
            frame.Append('│');
        }

        frame.MoveTo(X, Y + Height - 1);
        frame.SetColour(BorderColour, BackgroundColour);
        frame.Append('╰');
        frame.Append('─', innerWidth);
        frame.Append('╯');

        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }
}
