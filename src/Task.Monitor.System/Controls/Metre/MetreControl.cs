using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls.Chart;

namespace Task.Monitor.System.Controls.Metre;

/// <summary>
/// A horizontal metre rendering one or more series stacked left to right over the
/// range 0.0 - 1.0, i.e. a CPU metre carrying a Kernel % series and a User % series,
/// each drawn in its own colour. A legend row is printed directly below the metre.
/// </summary>
public sealed class MetreControl : Control
{
    // Braille dots occupy two columns; the left column is dots 1,2,3,7 (bits 0,1,2,6).
    //
    //   0/2  ⠀  U+2800  0x00  (empty)
    //   1/2  ⡇  U+2847  0x47  dots 1,2,3,7   (left column)
    //   2/2  ⣿  U+28FF  0xFF  all dots       (full)
    private static readonly char[] BrailleChars = ['⠀', '⡇', '⣿'];
    private static readonly char BarChar = '|';
    private static readonly char BlockChar = ' ';
    private static readonly char HalfBlockChar = '▌';

    private const int LegendSpacing = 2;

    private readonly List<MetreControlSeries> series = [];
    private readonly object seriesLock = new();
    private readonly AnsiScreenBuffer frame = new();

    // Ownership of each sub-cell of the metre; -1 marks an unfilled sub-cell.
    private int[] subCellOwners = [];

    private bool border = false;
    private int rows = 1;
    private bool showLegend = true;

    public MetreControl(ISystemTerminal terminal) : base(terminal) => Height = RequiredHeight;

    public MetreControlSeries AddSeries(string label, Color colour)
    {
        MetreControlSeries item = new(label, colour);

        lock (seriesLock) {
            series.Add(item);
        }

        return item;
    }

    public void AddSeries(MetreControlSeries item)
    {
        ArgumentNullException.ThrowIfNull(item, nameof(item));

        lock (seriesLock) {
            series.Add(item);
        }
    }

    // Distributes the filled portion of the metre across the series in declaration order.
    private static void AllocateSubCells(double[] values, int totalSubCells, int[] owners)
    {
        Array.Fill(owners, -1);

        double total = 0.0;

        for (int i = 0; i < values.Length; i++) {
            total += Math.Max(0.0, values[i]);
        }

        if (total <= 0.0 || totalSubCells <= 0) {
            return;
        }

        // The metre spans 0.0 - 1.0; anything past a full metre is truncated rather than rescaled.
        int fillSubCells = (int)Math.Round(Math.Min(total, 1.0) * totalSubCells);

        if (fillSubCells <= 0) {
            return;
        }

        // Largest remainder: hand out whole sub-cells first, then give the rounding slack to the
        // series with the biggest fractional part so the segments always sum to fillSubCells.
        int[] counts = new int[values.Length];
        double[] remainders = new double[values.Length];
        int assigned = 0;

        for (int i = 0; i < values.Length; i++) {
            double exact = Math.Max(0.0, values[i]) / total * fillSubCells;
            counts[i] = (int)exact;
            remainders[i] = exact - counts[i];
            assigned += counts[i];
        }

        for (int leftover = fillSubCells - assigned; leftover > 0; leftover--) {
            int best = 0;

            for (int i = 1; i < remainders.Length; i++) {
                if (remainders[i] > remainders[best]) {
                    best = i;
                }
            }

            counts[best]++;
            remainders[best] = double.NegativeInfinity;
        }

        int offset = 0;

        for (int i = 0; i < counts.Length && offset < totalSubCells; i++) {
            int span = Math.Min(counts[i], totalSubCells - offset);

            for (int s = 0; s < span; s++) {
                owners[offset + s] = i;
            }

            offset += span;
        }
    }

    private void AppendCell(int fill, Color colour)
    {
        switch (MetreStyle) {
            case MetreControlStyle.Blocks:
                if (fill >= SubCellsPerCell) {
                    frame.SetColour(ForegroundColour, colour);
                    frame.Append(BlockChar);
                }
                else {
                    frame.SetColour(colour, BackgroundColour);
                    frame.Append(HalfBlockChar);
                }

                break;

            case MetreControlStyle.Bars:
                frame.SetColour(colour, BackgroundColour);
                frame.Append(BarChar);
                break;

            default:
                frame.SetColour(colour, BackgroundColour);
                frame.Append(BrailleChars[Math.Clamp(fill, 0, BrailleChars.Length - 1)]);
                break;
        }
    }

    private void AppendEmptyCell()
    {
        frame.SetColour(ForegroundColour, BackgroundColour);
        frame.Append(MetreStyle == MetreControlStyle.Dots ? BrailleChars[0] : ' ');
    }

    private void AppendLegendRow(
        int innerWidth,
        Color[] colours,
        string[] labels,
        double[] values)
    {
        int remaining = innerWidth;

        for (int i = 0; i < labels.Length && remaining > 0; i++) {
            if (i > 0) {
                int spacing = Math.Min(LegendSpacing, remaining);
                frame.SetColour(ForegroundColour, BackgroundColour);
                frame.Append(' ', spacing);
                remaining -= spacing;

                if (remaining == 0) {
                    break;
                }
            }

            // A single cell in the metre style and colour, one space, then the label
            // with the series percentage appended.
            AppendCell(SubCellsPerCell, colours[i]);
            remaining--;

            if (remaining == 0) {
                break;
            }

            frame.SetColour(ForegroundColour, BackgroundColour);
            frame.Append(' ');
            remaining--;

            string text = FormatLegendPercentage(values[i]);

            if (labels[i].Length > 0) {
                text = $"{labels[i]} {text}";
            }

            int textLength = Math.Min(text.Length, remaining);
            frame.Append(text.AsSpan(0, textLength));
            remaining -= textLength;
        }

        frame.SetColour(ForegroundColour, BackgroundColour);
        frame.Append(' ', remaining);
    }

    private void AppendMetreRow(int innerWidth, Color[] colours)
    {
        int subCells = SubCellsPerCell;

        for (int col = 0; col < innerWidth; col++) {
            int start = col * subCells;
            int fill = 0;
            int owner = -1;
            int ownerSubCells = 0;

            for (int s = 0; s < subCells; s++) {
                int candidate = subCellOwners[start + s];

                if (candidate < 0) {
                    continue;
                }

                fill++;
                int candidateSubCells = 0;

                for (int t = 0; t < subCells; t++) {
                    if (subCellOwners[start + t] == candidate) {
                        candidateSubCells++;
                    }
                }

                // A cell straddling two series is drawn in the colour of the dominant one.
                if (candidateSubCells > ownerSubCells) {
                    ownerSubCells = candidateSubCells;
                    owner = candidate;
                }
            }

            if (owner < 0) {
                AppendEmptyCell();
                continue;
            }

            AppendCell(fill, colours[owner]);
        }
    }

    /// <summary>
    /// Draws a border around the control, consuming a row above and below the metre.
    /// Toggling this resets <see cref="Control.Height"/> to <see cref="RequiredHeight"/>.
    /// </summary>
    public bool Border
    {
        get => border;
        set {
            if (border != value) {
                border = value;
                Height = RequiredHeight;
            }
        }
    }

    public Color BorderColour { get; set; } = ConsolePalette.White;

    public void ClearSeries()
    {
        lock (seriesLock) {
            series.Clear();
        }
    }

    private void DrawBottomBorder(int y, int innerWidth)
    {
        string labelPadded = Text.Length > 0 ? $" {Text} " : string.Empty;
        int labelLength = Math.Min(labelPadded.Length, innerWidth);
        int leftDashes = (innerWidth - labelLength) / 2;
        int rightDashes = innerWidth - labelLength - leftDashes;

        frame.MoveTo(X, y);
        frame.SetColour(BorderColour, BackgroundColour);
        frame.Append('╰');
        frame.Append('─', leftDashes);
        frame.SetColour(ForegroundColour, BackgroundColour);
        frame.Append(labelPadded.AsSpan(0, labelLength));
        frame.SetColour(BorderColour, BackgroundColour);
        frame.Append('─', rightDashes);
        frame.Append('╯');
    }

    private void DrawTopBorder(int y, int innerWidth)
    {
        frame.MoveTo(X, y);
        frame.SetColour(BorderColour, BackgroundColour);
        frame.Append('╭');
        frame.Append('─', innerWidth);
        frame.Append('╮');
    }

    /// <summary>
    /// Formats a series value for the legend. Values span the metre's 0.0 - 1.0 range,
    /// so they are printed as a percentage.
    /// </summary>
    public static string FormatLegendPercentage(double value) => $"{value:0.0%}";

    /// <summary>
    /// The style used to draw the metre cells, matching the style applied to the Chart control.
    /// </summary>
    public MetreControlStyle MetreStyle { get; set; } = MetreControlStyle.Dots;

    protected override void OnDraw()
    {
        double[] values;
        Color[] colours;
        string[] labels;

        lock (seriesLock) {
            values = new double[series.Count];
            colours = new Color[series.Count];
            labels = new string[series.Count];

            for (int i = 0; i < series.Count; i++) {
                values[i] = series[i].Value;
                colours[i] = series[i].Colour;
                labels[i] = series[i].Label;
            }
        }

        int innerWidth = Border ? Width - 2 : Width;
        int innerHeight = Border ? Height - 2 : Height;
        int legendRows = ShowLegend && labels.Length > 0 ? 1 : 0;
        int metreRows = Math.Min(Rows, innerHeight - legendRows);

        if (innerWidth <= 0 || metreRows <= 0) {
            return;
        }

        int subCells = SubCellsPerCell;
        int totalSubCells = innerWidth * subCells;

        if (subCellOwners.Length != totalSubCells) {
            subCellOwners = new int[totalSubCells];
        }

        AllocateSubCells(values, totalSubCells, subCellOwners);

        frame.Clear();

        int y = Y;

        if (Border) {
            DrawTopBorder(y, innerWidth);
            y++;
        }

        for (int row = 0; row < metreRows; row++, y++) {
            frame.MoveTo(X, y);

            if (Border) {
                frame.SetColour(BorderColour, BackgroundColour);
                frame.Append('│');
            }

            AppendMetreRow(innerWidth, colours);

            if (Border) {
                frame.SetColour(BorderColour, BackgroundColour);
                frame.Append('│');
            }
        }

        if (legendRows > 0) {
            frame.MoveTo(X, y);

            if (Border) {
                frame.SetColour(BorderColour, BackgroundColour);
                frame.Append('│');
            }

            AppendLegendRow(innerWidth, colours, labels, values);

            if (Border) {
                frame.SetColour(BorderColour, BackgroundColour);
                frame.Append('│');
            }

            y++;
        }

        if (Border) {
            DrawBottomBorder(y, innerWidth);
        }

        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }

    /// <summary>
    /// The height the control needs to render <see cref="Rows"/> metre rows, the legend
    /// row and, when enabled, the border rows.
    /// </summary>
    public int RequiredHeight => Rows + (ShowLegend ? 1 : 0) + (Border ? 2 : 0);

    /// <summary>
    /// The height of the metre in rows, i.e. 1 row or 5 rows. Every row renders the same
    /// bar. Setting this resets <see cref="Control.Height"/> to <see cref="RequiredHeight"/>.
    /// </summary>
    public int Rows
    {
        get => rows;
        set {
            int clamped = Math.Max(1, value);

            if (rows != clamped) {
                rows = clamped;
                Height = RequiredHeight;
            }
        }
    }

    public IReadOnlyList<MetreControlSeries> Series
    {
        get {
            lock (seriesLock) {
                return series.ToArray();
            }
        }
    }

    public int SeriesCount
    {
        get {
            lock (seriesLock) {
                return series.Count;
            }
        }
    }

    public void SetValue(int index, double value)
    {
        lock (seriesLock) {
            ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, series.Count, nameof(index));

            series[index].Value = Math.Clamp(value, 0.0, 1.0);
        }

        Draw();
    }

    public void SetValues(params double[] values)
    {
        ArgumentNullException.ThrowIfNull(values, nameof(values));

        lock (seriesLock) {
            int count = Math.Min(values.Length, series.Count);

            for (int i = 0; i < count; i++) {
                series[i].Value = Math.Clamp(values[i], 0.0, 1.0);
            }
        }

        Draw();
    }

    /// <summary>
    /// Draws the legend row directly below the metre. Toggling this resets
    /// <see cref="Control.Height"/> to <see cref="RequiredHeight"/>.
    /// </summary>
    public bool ShowLegend
    {
        get => showLegend;
        set {
            if (showLegend != value) {
                showLegend = value;
                Height = RequiredHeight;
            }
        }
    }

    // Bars have no sub-cell resolution; braille and half blocks split a cell in two.
    private int SubCellsPerCell => MetreStyle == MetreControlStyle.Bars ? 1 : 2;

    public string Text { get; set; } = string.Empty;
}
