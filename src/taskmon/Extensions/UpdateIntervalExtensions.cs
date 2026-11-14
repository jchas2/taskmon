using System.Globalization;
using Task.Monitor.Configuration;
using Task.Monitor.Resources;

namespace Task.Monitor.Extensions;

public static class UpdateIntervalExtensions
{
    public static string CustomDisplayText(int milliseconds) =>
        string.Format(CultureInfo.CurrentCulture, Strings.UpdateIntervalCustom, milliseconds);

    public static UpdateInterval? FromMilliseconds(int milliseconds) =>
        Enum.IsDefined((UpdateInterval)milliseconds)
            ? (UpdateInterval)milliseconds
            : null;

    // EnumText's name for the interval; calling it as an extension here would recurse.
    public static string ToDisplayText(this UpdateInterval interval) =>
        string.Format(CultureInfo.CurrentCulture, Strings.UpdateIntervalDisplay, EnumText.ToDisplayText(interval), (int)interval);
}
