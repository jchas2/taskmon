using System.Diagnostics;
using System.Reflection;

namespace Task.Monitor.Cli.Utils;

public static class AssemblyVersionInfo
{
    public static string GetShortVersion() =>
        ReadVersion() is { } version
            ? $"{version.Major}.{version.Minor}"
            : "0.0";

    public static string GetVersion() =>
        ReadVersion() is { } version
            ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
            : "0.0.0.0";

    private static Version? ReadVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        Version? version  = assembly.GetName().Version;

        if (version == null) {
            Trace.WriteLine("Failed assembly.GetName().Version");
        }

        return version;
    }
}
