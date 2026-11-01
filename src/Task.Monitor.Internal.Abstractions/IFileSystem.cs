namespace Task.Monitor.Internal.Abstractions;

public interface IFileSystem
{
    public void   DeleteFile(string path);
    public bool   DirectoryExists(string? path);
    public bool   FileExists(string? path);
    public long GetFileLength(string path);
    string[]      GetFiles(string path);
    string[]      GetFiles(string path, string searchPattern);
    public string ReadAllText(string path);

    public void   ReplaceAllText(string path, string? contents);

    public bool   TryCreateDirectory(string path);

    public void   WriteAllText(string path, string? contents);

    public void   WriteAllTextAtomic(string path, string? contents);
}
