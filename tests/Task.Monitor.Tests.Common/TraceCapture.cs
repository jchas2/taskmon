using System.Collections.Concurrent;
using System.Diagnostics;

namespace Task.Monitor.Tests.Common;

// Collects every line written through Trace while it is attached. Trace is process wide, so tests
// running alongside also write here: assert on lines carrying something unique to the test.
public sealed class TraceCapture : TraceListener
{
    private readonly ConcurrentQueue<string> lines = new();

    public TraceCapture() => Trace.Listeners.Add(this);

    protected override void Dispose(bool disposing)
    {
        Trace.Listeners.Remove(this);
        base.Dispose(disposing);
    }

    public IReadOnlyList<string> Lines => lines.ToArray();

    public IReadOnlyList<string> LinesContaining(string text) =>
        lines.Where(line => line.Contains(text, StringComparison.Ordinal)).ToArray();

    public override void Write(string? message) => lines.Enqueue(message ?? string.Empty);

    public override void WriteLine(string? message) => lines.Enqueue(message ?? string.Empty);
}
