namespace Task.Monitor.Gui.Controls;

internal sealed class RollingPeak(int window, double floor)
{
    private readonly Queue<double> samples = new();

    public double Add(double value)
    {
        samples.Enqueue(Math.Max(0, value));

        while (samples.Count > window) {
            samples.Dequeue();
        }

        return Scale(value);
    }

    public void Clear() => samples.Clear();

    public double Scale(double value)
    {
        double peak = samples.Count > 0 
            ? samples.Max() 
            : 0;

        return Math.Clamp(Math.Max(0, value) / Math.Max(floor, peak), 0.0, 1.0);
    }
}
