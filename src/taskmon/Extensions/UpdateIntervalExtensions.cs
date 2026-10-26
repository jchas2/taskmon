using Task.Monitor.Configuration;

namespace Task.Monitor.Extensions;

public static class UpdateIntervalExtensions
{
    public static string CustomDisplayText(int milliseconds) => $"Custom ({milliseconds} ms)";

    public static UpdateInterval? FromMilliseconds(int milliseconds) =>
        Enum.IsDefined((UpdateInterval)milliseconds)
            ? (UpdateInterval)milliseconds
            : null;

    public static string ToDisplayText(this UpdateInterval interval) => $"{interval} ({(int)interval} ms)";
}
