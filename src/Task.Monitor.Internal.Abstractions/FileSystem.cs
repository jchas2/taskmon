namespace Task.Monitor.Internal.Abstractions;

public sealed class FileSystem : IFileSystem
{
    public bool Exists(string? path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string? contents) => File.WriteAllText(path, contents);
}
