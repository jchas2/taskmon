namespace Task.Monitor.Internal.Abstractions;

#pragma warning disable CA1416

public sealed partial class FileSystem
{
#if __WIN32__
    private bool CreateDirectoryInternal(string path)
    {
        Directory.CreateDirectory(path);
        return true;
    }
#endif
}

#pragma warning restore CA1416
