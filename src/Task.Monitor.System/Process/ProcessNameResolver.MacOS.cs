#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Process;

public sealed class ProcessNameResolver
{
    private const int DefaultArgMax  = 1024 * 1024;
    private const int MaxCachedNames = 4096;

    private static readonly IReadOnlyList<string> BundleInfoKeys = ["CFBundleExecutable"];

    private readonly Dictionary<string, string?>                                            bundleNames = new(StringComparer.Ordinal);
    private readonly Dictionary<(int Pid, long StartTicks), (string ExecutablePath, string Name)> names       = new();

    private byte[]? argumentsBuffer;

    internal static string? BundlePathFor(string executablePath)
    {
        string? macOS    = Path.GetDirectoryName(executablePath);
        string? contents = Path.GetDirectoryName(macOS);
        string? bundle   = Path.GetDirectoryName(contents);

        return Path.GetFileName(macOS) == "MacOS" &&
               Path.GetFileName(contents) == "Contents" &&
               bundle is not null &&
               bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
            ? bundle
            : null;
    }

    internal static string ChooseName(string executablePath, string? bundleName, string? launchPath, string kernelName)
    {
        if (bundleName is not null) {
            return bundleName;
        }

        if (executablePath.Length == 0) {
            return kernelName;
        }

        string fileName   = Path.GetFileName(executablePath);
        string launchName = Path.GetFileName(launchPath) ?? string.Empty;

        return launchName.Length > 0 && launchName != fileName
            ? launchName
            : fileName;
    }

    // Only the bundle's main executable takes its name: a crash handler beside it in Contents/MacOS doesn't.
    internal string? FindBundleName(string executablePath)
    {
        if (BundlePathFor(executablePath) is not { } bundlePath) {
            return null;
        }

        if (bundleNames.TryGetValue(executablePath, out string? name)) {
            return name;
        }

        if (bundleNames.Count >= MaxCachedNames) {
            bundleNames.Clear();
        }

        string? mainExecutable = PropertyList
            .ReadStrings(Path.Combine(bundlePath, "Contents", "Info.plist"), BundleInfoKeys)?
            .GetValueOrDefault("CFBundleExecutable") as string;

        name = mainExecutable == Path.GetFileName(executablePath)
            ? Path.GetFileNameWithoutExtension(bundlePath)
            : null;

        bundleNames[executablePath] = name;

        return name;
    }

    public string Resolve(int pid, long startTicks, string executablePath, string kernelName)
    {
        if (names.TryGetValue((pid, startTicks), out (string ExecutablePath, string Name) cached) &&
            cached.ExecutablePath == executablePath) {

            return cached.Name;
        }

        if (names.Count >= MaxCachedNames) {
            names.Clear();
        }

        string? launchPath = executablePath.Length > 0
            ? Sys.GetProcessExecPath(pid, argumentsBuffer ??= new byte[Sys.GetArgMax() ?? DefaultArgMax])
            : null;

        string name = ChooseName(executablePath, FindBundleName(executablePath), launchPath, kernelName);

        names[(pid, startTicks)] = (executablePath, name);

        return name;
    }
}
#endif
