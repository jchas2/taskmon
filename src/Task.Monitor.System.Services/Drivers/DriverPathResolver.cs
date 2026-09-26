namespace Task.Monitor.System.Services.Drivers;

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

        string windowsDir = windowsDirectory 
            ?? Environment.GetEnvironmentVariable("SystemRoot") 
            ?? @"C:\Windows";

        if (path.StartsWith(SystemRootToken, StringComparison.OrdinalIgnoreCase)) {
            return Path.Combine(windowsDir, path[SystemRootToken.Length..]);
        }

        if (Path.IsPathRooted(path)) {
            return path;
        }

        return path.Contains('\\') || path.Contains('/')
            ? Path.Combine(windowsDir, path)
            : Path.Combine(windowsDir, "System32", "drivers", path);
    }
}
