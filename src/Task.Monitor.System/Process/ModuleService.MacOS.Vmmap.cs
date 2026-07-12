using System.Diagnostics;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Process;

public partial class ModuleService
{
#if __APPLE__
    private const string VmmapPath = "/usr/bin/vmmap";
    private const int VmmapTimeoutMs = 3000;

    // Initial path for process modules as the call to task_for_pid can't open SIP-protected / hardened apps.
    // Shell out to Apple's vmmap, which is signed with com.apple.system-task-ports.
    private static bool GetModulesFromVmmap(int pid, List<ModuleInfo> moduleInfos)
    {
        try {
            using global::System.Diagnostics.Process proc = new() {
                StartInfo = new global::System.Diagnostics.ProcessStartInfo {
                FileName = VmmapPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true }
            };

            proc.StartInfo.ArgumentList.Add(pid.ToString());

            if (!proc.Start()) {
                Trace.WriteLine($"Failed to start {VmmapPath}");
                return false;
            }

            HashSet<string> seen = new(StringComparer.Ordinal);

            // Each loaded Mach-O image has a "__TEXT" (code) region row in vmmap's output
            // whose trailing token is the backing file path.
            for (string? line = proc.StandardOutput.ReadLine(); line is not null; line = proc.StandardOutput.ReadLine()) {
                if (!line.StartsWith("__TEXT", StringComparison.Ordinal)) {
                    continue;
                }

                string? path = ExtractTrailingPath(line);

                if (!string.IsNullOrEmpty(path) && seen.Add(path)) {
                    moduleInfos.Add(new ModuleInfo {
                        FileName = path,
                        ModuleName = Path.GetFileName(path)
                    });
                }
            }

            // Drain stderr so vmmap can't block on a full pipe.
            _ = proc.StandardError.ReadToEnd();

            if (!proc.WaitForExit(VmmapTimeoutMs)) {
                Trace.WriteLine($"Timeout of {VmmapTimeoutMs} expired for {VmmapPath}.");
                proc.Kill(entireProcessTree: true);
            }

            return moduleInfos.Count > 0;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"An Exception occurred processing {VmmapPath}");
            return false;
        }
    }

    private static string? ExtractTrailingPath(string line)
    {
        int index = line.IndexOf(" /", StringComparison.Ordinal);
        return index < 0 ? null : line[(index + 1)..].Trim();
    }
#endif
}
