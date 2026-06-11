using System.Globalization;
using Task.Monitor.System.Configuration;

namespace Task.Monitor.Configuration;

public sealed class Layout
{
    private ConfigSection? layoutSection;
    private const string ChartArray = "0,1,2,3,4,5,6,7";
    
    public Layout() { }

    public Layout(ConfigSection configSection) => layoutSection = configSection;

    public string Name => layoutSection?.Name ?? string.Empty;

    public void Update(ConfigSection configSection) => layoutSection = configSection;

    public float Ratio
    {
        get => layoutSection?.GetFloat(Constants.Keys.Ratio, 0.5f) ?? 0.5f;
        set => layoutSection?.Add(Constants.Keys.Ratio, Math.Clamp(value, 0.0f, 1.0f).ToString(CultureInfo.CurrentCulture));
    }

    public int Rows
    {
        get => layoutSection?.GetInt(Constants.Keys.NumRows, 2) ?? 2;
        set => layoutSection?.Add(Constants.Keys.NumRows, Math.Clamp(value, 1, 2).ToString(CultureInfo.CurrentCulture));
    }
    
    public int Cols
    {
        get => layoutSection?.GetInt(Constants.Keys.NumCols, 4) ?? 4;
        set => layoutSection?.Add(Constants.Keys.NumCols, Math.Clamp(value, 1, 4).ToString(CultureInfo.CurrentCulture));
    }

    public List<int> Charts
    {
        get {
            string charts = layoutSection?.GetString(Constants.Keys.Charts, ChartArray) ?? ChartArray;
            List<int> chartIndexes = charts.Split(',')
                .Where(str => int.TryParse(str, out _))
                .Select(str => int.Parse(str))
                .ToList();

            return chartIndexes;
        }
        set => layoutSection?.Add(Constants.Keys.Charts, string.Join(',', value));
    }
}
