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

        return Math.Clamp(Math.Max(0, value) / Math.Max(floor, samples.Max()), 0.0, 1.0);
    }

    public void Clear() => samples.Clear();
}
