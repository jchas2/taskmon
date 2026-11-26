using System.Drawing;
using Task.Monitor.Configuration;

namespace Task.Monitor.Extensions;

public static class TextMetreExtensions
{
    public const int TextMetreCells = 10;

    private const char   FilledCell        = '⣿';
    private const double MidLoadThreshold  = 0.5;
    private const double HighLoadThreshold = 0.85;

    public static string ToTextMetre(this double ratio)
    {
        double clamped = double.IsNaN(ratio) ? 0.0 : Math.Clamp(ratio, 0.0, 1.0);
        int    cells   = (int)Math.Round(clamped * TextMetreCells, MidpointRounding.AwayFromZero);

        if (clamped > 0 && cells == 0) {
            cells = 1;
        }

        return new string(FilledCell, cells).PadRight(TextMetreCells);
    }

    public static Color ToTextMetreColour(this double ratio, Theme theme) => ratio switch {
        >= HighLoadThreshold => theme.StatusError,
        >= MidLoadThreshold  => theme.StatusWarning,
                           _ => theme.StatusOk
    };
}
