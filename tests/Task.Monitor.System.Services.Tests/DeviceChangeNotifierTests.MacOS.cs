#if __APPLE__
using Task.Monitor.System.Services;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Services.Tests;

public sealed partial class DeviceChangeNotifierTests
{
    private const string HdiUtil = "/usr/bin/hdiutil";

    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(10);
    private static readonly char[]   OutputSeparators    = ['\n', '\t', ' '];

    // A disk image attached without mounting is a new disk to IOKit, as a USB stick would be, without
    // needing any hardware.
    [SkippableFact]
    public void DeviceChanged_Raises_Storage_When_A_Disk_Image_Attaches_And_Detaches()
    {
        Skip.IfNot(File.Exists(HdiUtil), $"{HdiUtil} is not available");

        string folder = Path.Combine(Path.GetTempPath(), $"taskmon-{Guid.NewGuid():N}");
        string image  = Path.Combine(folder, "device-change.dmg");

        Directory.CreateDirectory(folder);

        using ManualResetEventSlim storageChanged = new(initialState: false);
        DeviceChangeNotifier       notifier       = new();

        notifier.DeviceChanged += category => {
            if (category == DeviceCategory.Storage) {
                storageChanged.Set();
            }
        };

        string? device = null;

        try {
            (int created, _) = RunHdiUtil("create", "-size", "4m", "-fs", "HFS+", "-volname", "taskmon-test", image);
            Skip.If(created != 0, "hdiutil could not create a disk image here");

            notifier.Start();

            (int attached, string output) = RunHdiUtil("attach", "-nomount", image);
            Skip.If(attached != 0, "hdiutil could not attach a disk image here");

            device = output.Split(OutputSeparators, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(word => word.StartsWith("/dev/disk", StringComparison.Ordinal));

            Assert.NotNull(device);
            Assert.True(storageChanged.Wait(NotificationTimeout), "attaching should raise Storage");

            storageChanged.Reset();
            RunHdiUtil("detach", device);
            device = null;

            Assert.True(storageChanged.Wait(NotificationTimeout), "detaching should raise Storage");
        }
        finally {
            if (device != null) {
                RunHdiUtil("detach", "-force", device);
            }

            notifier.Dispose();
            Directory.Delete(folder, recursive: true);
        }
    }

    private static (int ExitCode, string Output) RunHdiUtil(params string[] arguments)
    {
        SysDiag::ProcessStartInfo startInfo = new(HdiUtil) {
            RedirectStandardOutput = true,
            RedirectStandardError  = true
        };

        foreach (string argument in arguments) {
            startInfo.ArgumentList.Add(argument);
        }

        using SysDiag::Process process = SysDiag::Process.Start(startInfo)!;

        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output);
    }
}
#endif
