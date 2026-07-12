namespace Task.Monitor.System;

public static partial class PathPermissions
{
#if __WIN32__
    private static void EnsureUserOwnershipInternal(string path)
    {
    }
#endif
}
