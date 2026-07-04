namespace Task.Monitor.Internal.Abstractions;

public interface IFileSystem
{
    public bool TryCreateDirectory(string path);
    public bool DirectoryExists(string? path);
    public bool FileExists(string? path);
    string[] GetFiles(string path);
    public string ReadAllText(string path);
    public void WriteAllText(string path, string? contents);
}
