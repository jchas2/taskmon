namespace Task.Monitor.Internal.Abstractions;

#pragma warning disable CA1416

public sealed partial class FileSystem
{
#if __APPLE__
    private bool CreateDirectoryInternal(string path)
    {
        UnixFileMode sharedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        Directory.CreateDirectory(path, sharedMode);
        return true;
    }    
#endif
}

#pragma warning restore CA1416
