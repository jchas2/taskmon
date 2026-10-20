#if __APPLE__
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.MacOS;

public sealed class TargetUser
{
    private const int PasswdBufferSize = 1024;

    public static TargetUser Current()
    {
        uint euid = UniStd.geteuid();
        uint uid  = UniStd.getuid();

        uint targetUid = ResolveUid(
            euid,
            uid,
            Environment.GetEnvironmentVariable("SUDO_UID"),
            euid == 0 ? SystemConfiguration.GetConsoleUserId() : null);

        return new TargetUser {
            Uid            = targetUid,
            IsRoot         = euid == 0,
            IsSessionOwner = targetUid == uid,
            HomeDirectory  = GetHomeDirectory(targetUid) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Uuid           = Membership.GetUserUuid(targetUid)
        };
    }

    private static unsafe string? GetHomeDirectory(uint uid)
    {
        byte* buffer = stackalloc byte[PasswdBufferSize];

        if (Pwd.GetPwUidR(
            uid, 
            out Pwd.Passwd passwd, 
            buffer, 
            PasswdBufferSize) != 0 || passwd.HomeDirectory == null) {
            
            return null;
        }

        string? home = Marshal.PtrToStringUTF8((IntPtr)passwd.HomeDirectory);
        
        return string.IsNullOrEmpty(home) 
            ? null 
            : home;
    }

    public string  HomeDirectory  { get; private init; } = string.Empty;

    public bool    IsRoot         { get; private init; }

    public bool IsSessionOwner    { get; private init; }

    public static uint ResolveUid(
        uint    euid, 
        uint    uid, 
        string? sudoUid, 
        uint?   consoleUid)
    {
        if (euid != 0) {
            return uid;
        }

        if (uint.TryParse(sudoUid, out uint sudoer) && sudoer != 0) {
            return sudoer;
        }

        return consoleUid is > 0 ? consoleUid.Value : uid;
    }

    public uint    Uid            { get; private init; }

    public string? Uuid           { get; private init; }
}
#endif
