using System.Collections.Concurrent;
using System.Diagnostics;

namespace Task.Monitor.Cli.Utils;

public static class TraceEx
{
    private static readonly ConcurrentDictionary<string, DateTime> lastSeen = new();
    private static TimeSpan suppressionWindow = TimeSpan.FromMinutes(10);

    public static void WriteLineOnce(string key, string message)
    {
        DateTime now = DateTime.UtcNow;
        lastSeen.AddOrUpdate(
            key,
            addValueFactory: k => {
                Trace.WriteLine($"Key: {key}: {message}");
                return now;
            },
            updateValueFactory: (k, lastLoggedTime) => {
                if (now - lastLoggedTime >= suppressionWindow) {
                    Trace.WriteLine($"Key: {key}: {message}");
                    return now;
                }
                return lastLoggedTime;
            }
        );
    }
}
