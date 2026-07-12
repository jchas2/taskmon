using Task.Monitor.Cli.Utils;

namespace Task.Monitor.Internal.Abstractions;

public sealed partial class FileSystem : IFileSystem
{
    public bool DirectoryExists(string? path) => Directory.Exists(path);

    public bool FileExists(string? path) => File.Exists(path);

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
    
    public string ReadAllText(string path) => File.ReadAllText(path);

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

    public void WriteAllText(string path, string? contents) => File.WriteAllText(path, contents);
}
#pragma warning restore CA1416
