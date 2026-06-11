namespace Task.Monitor.System.Process;

[Flags]
public enum UserRoleFlags
{
    RootUser   = 1 << 0,     // User account is running as root.
    OtherUser  = 1 << 1,     // User account is different to the current account.
    SystemUser = 1 << 2     // User account is a system or service account.
}
