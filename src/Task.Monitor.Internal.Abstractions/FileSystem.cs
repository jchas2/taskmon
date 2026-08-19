using Task.Monitor.Cli.Utils;

namespace Task.Monitor.Internal.Abstractions;

public sealed partial class FileSystem : IFileSystem
{
    public bool DirectoryExists(string? path) => Directory.Exists(path);

    public bool FileExists(string? path) => File.Exists(path);

    public long GetFileLength(string path)
    {
        try {
            FileInfo finfo = new(path);
            return finfo.Length;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"Error on GetFileLength '{path}'");
            return -1;
        }
    }

    public string[] GetFiles(string path)
    {
        try {
            return Directory.GetFiles(path);
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"Error on GetFiles '{path}'");
            return [];
        }
    }

    public string ReadAllText(string path)
    {
        try {
            return File.ReadAllText(path);
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"Error on ReadAllText '{path}'");
            throw;
        }
    } 

    public bool TryCreateDirectory(string path)
    {
        try {
            return DirectoryExists(path) || CreateDirectoryInternal(path);
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"Error creating directory '{path}'");
            return false;
        }
    }

    public void WriteAllText(string path, string? contents)
    {
        try {
            File.WriteAllText(path, contents);
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"Error on WriteAllText '{path}'");
            throw;
        }
    } 
}
#pragma warning restore CA1416
