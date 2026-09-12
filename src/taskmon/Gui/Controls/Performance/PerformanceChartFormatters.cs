namespace Task.Monitor.Gui.Controls.Performance;

public static class PerformanceChartFormatters
{
    // A chart's Y axis gutter is a fixed five columns, so the scale labels use this compact form
    // while the chart title and the list views carry the full "9.8 MB/s". Shared by the disk and
    // network screens, which both plot an unbounded byte rate.
    public static string FormatYScaleByteRate(double bytesPerSecond)
    {
        if (bytesPerSecond < 1.0) {
            return "0";
        }

        string[] rateFormatters = ["", "K", "M", "G", "T"];
        int index = 0;
        double rate = bytesPerSecond;

        while (rate >= 1024.0 && index < rateFormatters.Length - 1) {
            index++;
            rate /= 1024.0;
        }

        return index == 0 || rate >= 100.0
            ? $"{rate:0}{rateFormatters[index]}"
            : $"{rate:0.#}{rateFormatters[index]}";
    }
}
