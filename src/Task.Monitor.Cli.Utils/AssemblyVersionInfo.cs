using System.Diagnostics;
using System.Reflection;

namespace Task.Monitor.Cli.Utils;

public static class AssemblyVersionInfo
{
    public static string GetVersion()
    {
        const string NoVersion = "0.0.0.0";

        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        Version? version = assembly.GetName().Version;
        
        if (version == null) {
            Trace.WriteLine("Failed assembly.GetName().Version");
            return NoVersion;
        }

        return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
}
