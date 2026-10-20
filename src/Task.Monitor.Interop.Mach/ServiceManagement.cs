using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class ServiceManagement
{
    public enum LegacyServiceStatus : long
    {
        NotRegistered    = 0,
        Enabled          = 1,
        RequiresApproval = 2,
        NotFound         = 3
    }

    private static readonly Lazy<(nint Class, nint Selector)> StatusForLegacyUrl = new(() => {
        // Loading the ServiceManagement framework registers its classes with the objc runtime.
        if (!NativeLibrary.TryLoad(Libraries.ServiceManagement, out _)) {
            return (nint.Zero, nint.Zero);
        }

        nint smAppService = ObjCRuntime.objc_getClass("SMAppService");

        return smAppService == nint.Zero
            ? (nint.Zero, nint.Zero)
            : (smAppService, ObjCRuntime.sel_registerName("statusForLegacyURL:"));
    });

    private static readonly Lazy<nint> UserDomain = new(() =>
        DyLib.ReadExportedPointer(Libraries.ServiceManagement, "kSMDomainUserLaunchd"));

    private static readonly Lazy<nint> SystemDomain = new(() =>
        DyLib.ReadExportedPointer(Libraries.ServiceManagement, "kSMDomainSystemLaunchd"));

    public static LegacyServiceStatus? GetLegacyStatus(string plistPath)
    {
        (nint smAppService, nint selector) = StatusForLegacyUrl.Value;

        if (smAppService == nint.Zero) {
            return null;
        }

        using CFScope url = new(CoreFoundation.CFURLCreateFromPath(plistPath, isDirectory: false));

        if (url.IsNull) {
            return null;
        }

        nint pool = ObjCRuntime.objc_autoreleasePoolPush();

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

    private static bool IsLoaded(nint domain, string label)
    {
        if (domain == nint.Zero || label.Length == 0) {
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

    public static bool IsLoadedInSystemDomain(string label) => IsLoaded(SystemDomain.Value, label);

    public static bool IsLoadedInUserDomain(string label) => IsLoaded(UserDomain.Value, label);

    [DllImport(Libraries.ServiceManagement)]
    private static extern nint SMJobCopyDictionary(nint domain, nint jobLabel);
}
