using System.Runtime.CompilerServices;

namespace Task.Monitor.Tests.Common;

public sealed class FileCleanupHelper : IDisposable
{
    private List<string> tempDirs = new();
    private Lock @lock = new();

    ~FileCleanupHelper() => Dispose();

    public void Dispose()
    {
        foreach (string dir in tempDirs) {
            try {
                try {
                    Directory.Delete(dir, recursive: true);
                }
                catch (UnauthorizedAccessException) {
                    DirectoryInfo dirInfo = new(dir);
                    foreach (FileSystemInfo fileInfo in dirInfo.EnumerateFileSystemInfos("*",
                                 SearchOption.AllDirectories)) {
                        fileInfo.Attributes = FileAttributes.Normal;
                    }

                    Directory.Delete(dir, recursive: true);
                }
                catch (DirectoryNotFoundException) { }
            }
            catch { }
        }
    }

    private string GetTempDirectoryInternal([CallerMemberName] string memberName = "")
    {
        lock (@lock) {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"{memberName}-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDirectory);
            tempDirs.Add(tempDirectory);
            return tempDirectory;
        }
    }

    public string GetTempDirectory([CallerMemberName] string memberName = "") => GetTempDirectoryInternal(memberName);

    public string GetTempFile(string extension)
    {
        if (!extension.StartsWith('.')) {
            extension = '.' + extension;
        }

        string tempFile = Path.Combine(
            GetTempDirectoryInternal(),
            Path.GetRandomFileName() + extension);

        return tempFile;
    }
}