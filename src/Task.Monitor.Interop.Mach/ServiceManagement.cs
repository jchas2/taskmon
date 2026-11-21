using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class ServiceManagement
{
    public const string AppServiceClass            = "SMAppService";
    public const string StatusForLegacyUrlSelector = "statusForLegacyURL:";
    public const string kSMDomainSystemLaunchd     = "kSMDomainSystemLaunchd";
    public const string kSMDomainUserLaunchd       = "kSMDomainUserLaunchd";

    public enum LegacyServiceStatus : long
    {
        NotRegistered    = 0,
        Enabled          = 1,
        RequiresApproval = 2,
        NotFound         = 3
    }

    [DllImport(Libraries.ServiceManagement)]
    public static extern nint SMCopyAllJobDictionaries(nint domain);

    [DllImport(Libraries.ServiceManagement)]
    public static extern nint SMJobCopyDictionary(nint domain, nint jobLabel);
}
