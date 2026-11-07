#if __APPLE__
using System.Diagnostics;
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Common;
using Task.Monitor.System.Services.LaunchdServices;

namespace Task.Monitor.System.Services.Startup;

public partial class StartupService
{
    private const int LaunchctlNotPermitted = 1;
    private const int OverrideWaitMs        = 250;

    private partial StartupToggleResult ApplyEnabled(StartupEntry entry, bool enabled)
    {
        if (CheckCanSetEnabled(entry, enabled, UniStd.geteuid() == 0, PathExists) is { } refused) {
            return refused;
        }

        LaunchdTarget target = entry.Launchd!.Value;

        StartupToggleResult result = ToToggleResult(
            Launchctl.Run(Launchctl.BuildSetEnabledArguments(target.ServiceTarget, enabled)));

        if (result == StartupToggleResult.Succeeded) {
            WaitForOverride(target, enabled);
        }

        return result;
    }

    internal static StartupToggleResult? CheckCanSetEnabled(
        StartupEntry       entry,
        bool               enabled,
        bool               isRoot,
        Func<string, bool> exists)
    {
        if (entry.Launchd is not { } target) {
            return StartupToggleResult.NotSupported;
        }

        // launchctl enable can't override "Allow in the Background" being off in System Settings.
        if (enabled && target.BlockedByBackgroundItems) {
            return StartupToggleResult.RequiresApproval;
        }

        if (!exists(entry.Origin)) {
            return StartupToggleResult.NotFound;
        }

        if (target.Domain == LaunchdDomain.System && !isRoot) {
            return StartupToggleResult.AccessDenied;
        }

        return null;
    }

    internal static bool HasOverride(LaunchdOverrides overrides, string label, bool enabled) =>
        overrides.Contains(label) && overrides.IsDisabled(label, plistDisabled: null) != enabled;

    private static bool PathExists(string path) =>
        File.Exists(path) || Directory.Exists(path);

    private static LaunchdOverrides ReadOverrides(LaunchdTarget target) =>
        target.Domain == LaunchdDomain.System
            ? LaunchdOverrides.ReadSystem()
            : LaunchdOverrides.ReadUser(target.Uid);

    internal static StartupToggleResult ToToggleResult(int exitCode) => exitCode switch {
        0                     => StartupToggleResult.Succeeded,
        LaunchctlNotPermitted => StartupToggleResult.AccessDenied,
                            _ => StartupToggleResult.Failed
    };

    // launchd writes its override file a few ms after launchctl exits, and the rescan reads that file.
    private static void WaitForOverride(LaunchdTarget target, bool enabled)
    {
        long deadline = Environment.TickCount64 + OverrideWaitMs;

        while (!HasOverride(ReadOverrides(target), target.Label, enabled)) {
            if (Environment.TickCount64 >= deadline) {
                Trace.WriteLine($"{nameof(WaitForOverride)} {target.ServiceTarget}: not written after {OverrideWaitMs} ms");
                return;
            }

            Thread.Sleep(1);
        }
    }
}
#endif
