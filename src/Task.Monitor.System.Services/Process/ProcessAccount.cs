namespace Task.Monitor.System.Services.Process;

// The kind of account a process runs as: an ordinary user, a system or service account, or the
// all-powerful account (root on macOS, SYSTEM on Windows).
public enum ProcessAccount
{
    User,
    System,
    Root
}

public static class ProcessAccounts
{
    // macOS gives people uids from 501, and system accounts (the _-prefixed daemons) uids below
    // 500. "nobody" is -2, stored as an unsigned uid.
    private const uint FirstUserUid = 500;
    private const uint NobodyUid    = 4294967294;

    // Windows: LocalSystem, LocalService and NetworkService, then the prefixes of per-service
    // accounts (NT SERVICE\...), Window Manager accounts (DWM-n) and font driver hosts (UMFD-n).
    private const string LocalSystemSid    = "S-1-5-18";
    private const string LocalServiceSid   = "S-1-5-19";
    private const string NetworkServiceSid = "S-1-5-20";

    private static readonly string[] SystemSidPrefixes = ["S-1-5-80-", "S-1-5-90-", "S-1-5-96-"];

    public static ProcessAccount FromSid(string sid) => sid switch {
        LocalSystemSid                                                 => ProcessAccount.Root,
        LocalServiceSid or NetworkServiceSid                           => ProcessAccount.System,
        _ when SystemSidPrefixes.Any(prefix =>
                   sid.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) => ProcessAccount.System,
                                                                     _ => ProcessAccount.User
    };

    public static ProcessAccount FromUid(uint uid) => uid switch {
        0                                    => ProcessAccount.Root,
        < FirstUserUid or NobodyUid          => ProcessAccount.System,
                                           _ => ProcessAccount.User
    };
}
