#if __APPLE__
using System.Diagnostics;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Services.Common;

public static class Launchctl
{
    public const int NotRun = -1;

    private const string LaunchctlPath = "/bin/launchctl";
    private const int    TimeoutMs     = 5000;

    public static string[] BuildSetEnabledArguments(string serviceTarget, bool enabled) =>
        [enabled ? "enable" : "disable", serviceTarget];

    public static int Run(IReadOnlyList<string> arguments)
    {
        string commandLine = $"{LaunchctlPath} {string.Join(' ', arguments)}";

        try {
            using global::System.Diagnostics.Process proc = new() {
                StartInfo = new ProcessStartInfo {
                    FileName               = LaunchctlPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                }
            };

            foreach (string argument in arguments) {
                proc.StartInfo.ArgumentList.Add(argument);
            }

            Trace.WriteLine($"Spawn {commandLine}");

            if (!proc.Start()) {
                Trace.WriteLine($"Failed to start {commandLine}");
                return NotRun;
            }

            _ = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd().Trim();

            if (!proc.WaitForExit(TimeoutMs)) {
                Trace.WriteLine($"Timeout of {TimeoutMs} expired for {commandLine}.");
                proc.Kill(entireProcessTree: true);
                return NotRun;
            }

            if (proc.ExitCode != 0) {
                Trace.WriteLine($"{commandLine} exited with {proc.ExitCode}: {error}");
            }

            return proc.ExitCode;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"An Exception occurred running {commandLine}");
            return NotRun;
        }
    }
}
#endif
