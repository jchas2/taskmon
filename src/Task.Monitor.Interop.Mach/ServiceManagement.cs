using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

// ServiceManagement/SMJob.h and SMAppService.h. SMJobCopyDictionary is deprecated (10.10) but
// still answers, without root, whether a label is loaded in the user's or the system launchd
// domain. SMAppService (13.0+) is the supported API; it's Objective-C, so it's reached through
// the objc runtime.
public static class ServiceManagement
{
    // SMAppServiceStatus.
    public enum LegacyServiceStatus : long
    {
        NotRegistered    = 0,
        Enabled          = 1,
        RequiresApproval = 2,
        NotFound         = 3
    }

    private static readonly Lazy<(IntPtr Class, IntPtr Selector)> StatusForLegacyUrl = new(() => {
        // Loading the framework registers its classes with the objc runtime.
        if (!NativeLibrary.TryLoad(Libraries.ServiceManagement, out _)) {
            return (IntPtr.Zero, IntPtr.Zero);
        }

        // Nil before macOS 13, which has no SMAppService.
        IntPtr smAppService = ObjCRuntime.objc_getClass("SMAppService");

        return smAppService == IntPtr.Zero
            ? (IntPtr.Zero, IntPtr.Zero)
            : (smAppService, ObjCRuntime.sel_registerName("statusForLegacyURL:"));
    });

    private static readonly Lazy<IntPtr> UserDomain = new(() =>
        DyLib.ReadExportedPointer(Libraries.ServiceManagement, "kSMDomainUserLaunchd"));

    private static readonly Lazy<IntPtr> SystemDomain = new(() =>
        DyLib.ReadExportedPointer(Libraries.ServiceManagement, "kSMDomainSystemLaunchd"));

    [DllImport(Libraries.ServiceManagement)]
    private static extern IntPtr SMJobCopyDictionary(IntPtr domain, IntPtr jobLabel);

    public static bool IsLoadedInUserDomain(string label) => IsLoaded(UserDomain.Value, label);

    public static bool IsLoadedInSystemDomain(string label) => IsLoaded(SystemDomain.Value, label);

    // +[SMAppService statusForLegacyURL:]: the Background Task Management state of a legacy
    // launchd plist (e.g. one in /Library/LaunchDaemons), so RequiresApproval when the user has
    // switched it off under System Settings > Login Items > Allow in the Background. Answers for
    // the calling user's session. Null when the API is unavailable (before macOS 13).
    public static LegacyServiceStatus? GetLegacyStatus(string plistPath)
    {
        (IntPtr smAppService, IntPtr selector) = StatusForLegacyUrl.Value;

        if (smAppService == IntPtr.Zero) {
            return null;
        }

        // CFURLRef is toll-free bridged to NSURL.
        using CFScope url = new(CoreFoundation.CFURLCreateFromPath(plistPath, isDirectory: false));

        if (url.IsNull) {
            return null;
        }

        IntPtr pool = ObjCRuntime.objc_autoreleasePoolPush();

        try {
            long status = ObjCRuntime.objc_msgSend_Int64_IntPtr(smAppService, selector, url);

            return status is >= (long)LegacyServiceStatus.NotRegistered and <= (long)LegacyServiceStatus.NotFound
                ? (LegacyServiceStatus)status
                : null;
        }
        finally {
            ObjCRuntime.objc_autoreleasePoolPop(pool);
        }
    }

    private static bool IsLoaded(IntPtr domain, string label)
    {
        if (domain == IntPtr.Zero || label.Length == 0) {
            return false;
        }

        using CFScope cfLabel = new(CoreFoundation.CFStringCreate(label));

        try {
            using CFScope job = new(SMJobCopyDictionary(domain, cfLabel));

            return !job.IsNull;
        }
        catch (EntryPointNotFoundException) {
            return false;
        }
    }
}
