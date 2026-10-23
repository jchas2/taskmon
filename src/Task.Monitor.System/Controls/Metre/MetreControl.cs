using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Controls.Chart;

namespace Task.Monitor.System.Controls.Metre;

public sealed class MetreControl : Control
{
    private static readonly char[] BrailleChars  = ['⠀', '⡇', '⣿'];
    private static readonly char   BarChar       = '|';
    private static readonly char   BlockChar     = ' ';
    private static readonly char   HalfBlockChar = '▌';

    private const int LegendSpacing = 2;

    private readonly List<MetreControlSeries> series     = [];
    private readonly object                   seriesLock = new();
    private readonly AnsiScreenBuffer         frame      = new();

    private int[] subCellOwners = [];
    private bool  border        = false;
    private int   rows          = 1;
    private bool  showLegend    = true;

    private Color? borderBackgroundColour;

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

        int fillSubCells = (int)Math.Round(Math.Min(total, 1.0) * totalSubCells);

        if (fillSubCells <= 0) {
            return;
        }

        int[]    counts     = new int[values.Length];
        double[] remainders = new double[values.Length];
        int      assigned   = 0;

        for (int i = 0; i < values.Length; i++) {
            double exact = Math.Max(0.0, values[i]) / total * fillSubCells;
            
            counts[i]     = (int)exact;
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
        int      innerWidth,
        Color[]  colours,
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
            int start         = col * subCells;
            int fill          = 0;
            int owner         = -1;
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

    public Color BorderBackgroundColour
    {
        get => borderBackgroundColour ?? BackgroundColour;
        set => borderBackgroundColour = value;
    }

    public override Color BorderColour
    {
        get => BorderForegroundColour;
        set => BorderForegroundColour = value;
    }

    public Color BorderForegroundColour { get; set; } = ConsolePalette.White;

    public void ClearSeries()
    {
        lock (seriesLock) {
            series.Clear();
        }
    }

    private void DrawBorderEdge(int y, int innerWidth, char left, char right, bool withText)
    {
        string labelPadded = withText && Text.Length > 0 
            ? $" {Text} " 
            : string.Empty;
        
        int labelLength = Math.Min(labelPadded.Length, innerWidth);
        int leftDashes  = TextPosition == MetreTextPosition.TopLeft
            ? Math.Min(1, innerWidth - labelLength)
            : (innerWidth - labelLength) / 2;
        int rightDashes = innerWidth - labelLength - leftDashes;

        frame.MoveTo(X, y);
        SetBorderColour();
        frame.Append(left);
        frame.Append('─', leftDashes);
        frame.SetColour(ForegroundColour, BorderBackgroundColour);
        frame.Append(labelPadded.AsSpan(0, labelLength));
        SetBorderColour();
        frame.Append('─', rightDashes);
        frame.Append(right);
    }

    private void DrawBottomBorder(int y, int innerWidth) =>
        DrawBorderEdge(y, innerWidth, '╰', '╯', withText: TextPosition == MetreTextPosition.BottomCentre);

    private void DrawTopBorder(int y, int innerWidth) =>
        DrawBorderEdge(y, innerWidth, '╭', '╮', withText: TextPosition != MetreTextPosition.BottomCentre);

    public static string FormatLegendPercentage(double value) => $"{value:0.0%}";

    public MetreControlStyle MetreStyle { get; set; } = MetreControlStyle.Dots;

    protected override void OnDraw()
    {
        Color[]  colours;
        double[] values;
        string[] labels;

        lock (seriesLock) {
            colours = new Color[series.Count];
            values  = new double[series.Count];
            labels  = new string[series.Count];

            for (int i = 0; i < series.Count; i++) {
                colours[i] = series[i].Colour;
                values[i]  = series[i].Value;
                labels[i]  = series[i].Label;
            }
        }

        int innerWidth  = Border ? Width - 2 : Width;
        int innerHeight = Border ? Height - 2 : Height;
        int legendRows  = ShowLegend && labels.Length > 0 ? 1 : 0;
        int metreRows   = Math.Min(Rows, innerHeight - legendRows);

        if (innerWidth <= 0 || metreRows <= 0) {
            return;
        }

        int subCells      = SubCellsPerCell;
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
                SetBorderColour();
                frame.Append('│');
            }

            AppendMetreRow(innerWidth, colours);

            if (Border) {
                SetBorderColour();
                frame.Append('│');
            }
        }

        if (legendRows > 0) {
            frame.MoveTo(X, y);

            if (Border) {
                SetBorderColour();
                frame.Append('│');
            }

            AppendLegendRow(
                innerWidth, 
                colours, 
                labels, 
                values);

            if (Border) {
                SetBorderColour();
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

    public int RequiredHeight => Rows + (ShowLegend ? 1 : 0) + (Border ? 2 : 0);

    public int Rows
    {
        get => rows;
        set {
            int clamped = Math.Max(1, value);

            if (rows != clamped) {
                rows   = clamped;
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

    private void SetBorderColour() => frame.SetColour(DisplayBorderColour, BorderBackgroundColour);

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

    public bool ShowLegend
    {
        get => showLegend;
        set {
            if (showLegend != value) {
                showLegend = value;
                Height     = RequiredHeight;
            }
        }
    }

    private int SubCellsPerCell => MetreStyle == MetreControlStyle.Bars 
        ? 1 
        : 2;

    public string Text { get; set; } = string.Empty;

    public MetreTextPosition TextPosition { get; set; } = MetreTextPosition.BottomCentre;
}
