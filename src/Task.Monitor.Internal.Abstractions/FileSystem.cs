using Task.Monitor.Cli.Utils;

namespace Task.Monitor.Internal.Abstractions;

public sealed partial class FileSystem : IFileSystem
{
    private const string BackupExtension = ".bak";
    private const string TempExtension   = ".tmp";

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

    public string[] GetFiles(string path, string searchPattern)
    {
        try {
            return Directory.GetFiles(path, searchPattern);
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

    // Swaps the new text in whole, so a failed write can't leave a damaged file, and keeps the old
    // text beside it with a .bak extension. Symlinks are resolved first, as swapping over the link
    // would replace it with a plain file.
    public void ReplaceAllText(string path, string? contents)
    {
        string target   = File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? path;
        string tempPath = $"{target}{TempExtension}";

        try {
            File.WriteAllText(tempPath, contents);
            File.Replace(tempPath, target, $"{target}{BackupExtension}");
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"Error on ReplaceAllText '{path}'");
            throw;
        }
        finally {
            File.Delete(tempPath);
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
