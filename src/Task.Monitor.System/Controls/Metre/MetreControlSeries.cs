using System.Drawing;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Controls.Metre;

public sealed class MetreControlSeries
{
    public MetreControlSeries(string label, Color colour)
    {
        ArgumentNullException.ThrowIfNull(label, nameof(label));

        Label = label;
        Colour = colour;
    }

    public MetreControlSeries(
        string label,
        Color colour,
        double value)
        : this(label, colour) => Value = value;

    public Color Colour { get; set; } = ConsolePalette.White;

    public string Label { get; set; }

    // Updates flow through MetreControl.SetValue / SetValues so mutations stay under the control's lock.
    public double Value { get; internal set; } = 0.0;
}
