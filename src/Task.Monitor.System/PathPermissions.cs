namespace Task.Monitor.System;

public static partial class PathPermissions
{
    public static void EnsureUserOwnership(string path) => EnsureUserOwnershipInternal(path);
}
