namespace Task.Monitor.System.Services.Drivers;

// The Service Control Manager stores a driver's ImagePath in whatever form was registered, which
// is rarely the plain absolute path a Win32 service's usually is - all five of these forms show up
// on a real machine:
//   \SystemRoot\System32\drivers\xyz.sys   - relative to the Windows directory
//   \??\C:\Windows\System32\drivers\xyz.sys - an NT-namespace path (the \??\ prefix is a DOS-
//                                              device-path marker meaningful only to the kernel)
//   system32\drivers\xyz.sys                - relative to the Windows directory, just without the
//                                              leading backslash \SystemRoot\ would have had
//                                              (seen from real drivers - do not mistake this for
//                                              the bare-filename case below and double up the
//                                              System32\drivers segment)
//   xyz.sys                                 - a bare name with no directory component at all,
//                                              implicitly under System32\drivers
//   C:\Windows\System32\drivers\xyz.sys     - already a plain path (some drivers do register one)
// so a real, openable path has to be resolved before GetFileVersionInfo can read anything from it.
// Pure and platform-independent (just string handling) so it is testable without a real Windows
// registry or file system.
public static class DriverPathResolver
{
    private const string SystemRootToken = @"\SystemRoot\";
    private const string NtDevicePrefix = @"\??\";

    public static string? Expand(string? imagePath, string? windowsDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) {
            return null;
        }

        string path = imagePath.Trim();

        if (path.StartsWith(NtDevicePrefix, StringComparison.Ordinal)) {
            path = path[NtDevicePrefix.Length..];
        }

        string windowsDir = windowsDirectory ?? Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";

        if (path.StartsWith(SystemRootToken, StringComparison.OrdinalIgnoreCase)) {
            return Path.Combine(windowsDir, path[SystemRootToken.Length..]);
        }

        // A rooted path (drive letter, or already a UNC/NT path with no \SystemRoot\ token) is
        // used as-is.
        if (Path.IsPathRooted(path)) {
            return path;
        }

        // A relative path that already has its own directory component (e.g.
        // "system32\drivers\xyz.sys") is relative to the Windows directory itself - only a bare
        // filename with no directory component at all falls back to System32\drivers, the same
        // default the kernel applies when loading a driver by name alone.
        return path.Contains('\\') || path.Contains('/')
            ? Path.Combine(windowsDir, path)
            : Path.Combine(windowsDir, "System32", "drivers", path);
    }
}
