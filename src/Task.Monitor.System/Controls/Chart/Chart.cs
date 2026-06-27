using System.Drawing;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Controls.Chart;

public sealed class Chart : Control
{
    // Unicode offset from U+2800 is a bitmask: dot N occupies bit (N-1), except dots 7/8 = bits 6/7.
    //
    //   0/4  ⠀  U+2800  0x00  (empty)
    //   1/4  ⣀  U+28C0  0xC0  dots 7,8        (bottom row)
    //   2/4  ⣤  U+28E4  0xE4  dots 3,6,7,8
    //   3/4  ⣶  U+28F6  0xF6  dots 2,3,5,6,7,8
    //   4/4  ⣿  U+28FF  0xFF  all dots        (full)
    private static readonly char[] BrailleChars = ['\u2800', '\u28C0', '\u28E4', '\u28F6', '\u28FF'];
    private static readonly char BarChar = '|';
    private static readonly char BlockChar = ' ';
    
    private double[] data = [];
    private int dataHead  = 0;
    private int dataCount = 0;
    private double dataMax = 0.0;
    private readonly object dataLock = new();
    private readonly AnsiScreenBuffer frame = new();
    
    public Chart(ISystemTerminal terminal) : base(terminal) { }

    private int DataCapacity => Math.Max(0, Width - 2);

    public void Add(double value)
    {
        lock (dataLock) {
            AddInternal(value);
        }

        Draw();
    }

    private void AddInternal(double value)
    {
        if (data.Length == 0) {
            return;
        }

        if (dataCount < data.Length) {
            data[(dataHead + dataCount) % data.Length] = value;
            dataCount++;
            dataMax = Math.Max(dataMax, value);
        }
        else {
            double evicted = data[dataHead];
            data[dataHead] = value;
            dataHead = (dataHead + 1) % data.Length;

            if (value >= dataMax) {
                dataMax = value;
            }
            else if (evicted >= dataMax) {
                // The peak scrolled off — rescan the buffer for the new maximum.
                dataMax = 0.0;
                for (int i = 0; i < dataCount; i++) {
                    dataMax = Math.Max(dataMax, data[(dataHead + i) % data.Length]);
                }
            }
        }
    }

    public bool AutoScale { get; set; } = true;

    public MetreControlStyle MetreStyle { get; set; } = MetreControlStyle.Dots;
    
    public Color ColourHigh { get; set; } = ConsolePalette.Red;

    public Color ColourLow { get; set; } = ConsolePalette.DarkGreen;

    public Color ColourMid { get; set; } = ConsolePalette.Yellow;

    private double DataAt(int i) => data[(dataHead + i) % data.Length]; 
    
    public string LabelSeries { get; set; } = string.Empty;
    
    protected override void OnDraw()
    {
        double[] samples;
        double snapshotMax;

        lock (dataLock) {
            samples = new double[dataCount];

            for (int i = 0; i < dataCount; i++) {
                samples[i] = DataAt(i);
            }

            snapshotMax = dataMax;
        }

        int chartHeight = Math.Max(0, Height - 2);
        int chartWidth = Math.Max(0, Width - 2);
        int totalSubRows = chartHeight * 4;
        int sampleCount = samples.Length;

        double displayScale = AutoScale
            ? 1.0 / snapshotMax
            : 1.0;

        frame.Clear();
        frame.MoveTo(X, Y);
        frame.SetColour(ForegroundColour, BackgroundColour);
        frame.Append('\u256D');
        frame.Append('\u2500', chartWidth);
        frame.Append('\u256E');
        
        for (int row = 0; row < chartHeight; row++) {
            frame.MoveTo(X, Y + 1 + row);
            frame.SetColour(ForegroundColour, BackgroundColour);
            frame.Append('\u2502');

            int rowFromBottom = chartHeight - 1 - row;

            for (int col = 0; col < chartWidth; col++) {
                // Samples are right-aligned: the newest sample appears at the rightmost column.
                int sampleIndex = sampleCount - chartWidth + col;

                if (sampleIndex < 0) {
                    frame.SetColour(ForegroundColour, BackgroundColour);
                    frame.Append('\u2800');
                    continue;
                }

                double value = Math.Clamp(samples[sampleIndex] * displayScale, 0.0, 1.0);
                int barSubRows = (int)(value * totalSubRows);
                int fullRows = barSubRows / 4;
                int partialDots = barSubRows % 4;

                double ratio = (double)barSubRows / (double)totalSubRows;

                Color chartColour;

                if (ratio > 0.8) {
                    chartColour = ColourHigh;
                }
                else if (ratio > 0.5) {
                    chartColour = ColourMid;
                }
                else {
                    chartColour = ColourLow;
                }

                char ch = MetreStyle switch {
                    MetreControlStyle.Bars => BarChar,
                    MetreControlStyle.Blocks => BlockChar,
                    _ => BrailleChars[4]
                };

                if (rowFromBottom < fullRows) {
                    if (MetreStyle == MetreControlStyle.Dots) {
                        ch = BrailleChars[4];
                    }

                    SetCellColour(chartColour);
                }
                else if (rowFromBottom == fullRows && partialDots > 0) {
                    if (MetreStyle == MetreControlStyle.Dots) {
                        ch = BrailleChars[partialDots];
                    }

                    SetCellColour(chartColour);
                }
                else {
                    frame.SetColour(ForegroundColour, BackgroundColour);
                    ch = '\u2800';
                }

                frame.Append(ch);
            }

            frame.SetColour(ForegroundColour, BackgroundColour);
            frame.Append('\u2502');
        }

        frame.MoveTo(X, Y + Height - 1);
        frame.SetColour(ForegroundColour, BackgroundColour);

        string label = string.IsNullOrEmpty(LabelSeries)
            ? Text
            : $"{Text} {LabelSeries}";

        string labelPadded = label.Length > 0 ? $" {label} " : string.Empty;
        int labelLen = Math.Min(labelPadded.Length, chartWidth);
        int leftDashes = (chartWidth - labelLen) / 2;
        int rightDashes = chartWidth - labelLen - leftDashes;

        frame.Append('\u2570');
        frame.Append('\u2500', leftDashes);
        frame.Append(labelLen < labelPadded.Length ? labelPadded[..labelLen] : labelPadded);
        frame.Append('\u2500', rightDashes);
        frame.Append('\u256F');

        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }

    private void SetCellColour(Color chartColour) => frame.SetColour(
        MetreStyle == MetreControlStyle.Blocks ? ForegroundColour : chartColour,
        MetreStyle == MetreControlStyle.Blocks ? chartColour : BackgroundColour);

    protected override void OnResize()
    {
        lock (dataLock) {
            if (data.Length != DataCapacity) {
                double[] newData = new double[DataCapacity];
                int toCopy = Math.Min(dataCount, DataCapacity);

                for (int i = 0; i < toCopy; i++) {
                    newData[i] = DataAt(dataCount - toCopy + i);
                }

                data = newData;
                dataHead = 0;
                dataCount = toCopy;

                // Recompute max — older samples may have been dropped during truncation.
                dataMax = 0.0;
                
                for (int i = 0; i < dataCount; i++) {
                    dataMax = Math.Max(dataMax, data[i]);
                }
            }
        }
    }

    public string Text { get; set; } = string.Empty;
}
