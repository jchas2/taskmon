namespace Task.Monitor.Internal.Abstractions;

public interface IFileSystem 
{ 
    public bool Exists(string? path);
    public string ReadAllText(string path);
    public void WriteAllText(string path, string? contents);
}
