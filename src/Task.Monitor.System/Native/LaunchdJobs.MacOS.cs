#if __APPLE__
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public static class LaunchdJobs
{
    private static readonly Lazy<(nint Class, nint Selector)> StatusForLegacyUrl = new(() => {
        // Loading the ServiceManagement framework registers its classes with the objc runtime.
        if (!NativeLibrary.TryLoad(Libraries.ServiceManagement, out _)) {
            return (nint.Zero, nint.Zero);
        }

        nint smAppService = ObjCRuntime.objc_getClass(ServiceManagement.AppServiceClass);

        return smAppService == nint.Zero
            ? (nint.Zero, nint.Zero)
            : (smAppService, ObjCRuntime.sel_registerName(ServiceManagement.StatusForLegacyUrlSelector));
    });

    private static readonly Lazy<nint> SystemDomain = new(() =>
        NativeExports.ReadPointer(Libraries.ServiceManagement, ServiceManagement.kSMDomainSystemLaunchd));

    private static readonly Lazy<nint> UserDomain = new(() =>
        NativeExports.ReadPointer(Libraries.ServiceManagement, ServiceManagement.kSMDomainUserLaunchd));

    private static List<Dictionary<string, object?>>? GetJobs(nint domain)
    {
        if (domain == nint.Zero) {
            return null;
        }

        try {
            using CFScope jobs = new(ServiceManagement.SMCopyAllJobDictionaries(domain));

            return PropertyList.ToManaged(jobs) is List<object?> list
                ? list.OfType<Dictionary<string, object?>>().ToList()
                : null;
        }
        catch (EntryPointNotFoundException) {
            return null;
        }
    }

    public static ServiceManagement.LegacyServiceStatus? GetLegacyStatus(string plistPath)
    {
        (nint smAppService, nint selector) = StatusForLegacyUrl.Value;

        if (smAppService == nint.Zero) {
            return null;
        }

        using CFScope url = new(CFConvert.CreateFileUrl(plistPath, isDirectory: false));

        if (url.IsNull) {
            return null;
        }

        nint pool = ObjCRuntime.objc_autoreleasePoolPush();

        try {
            long status = ObjCRuntime.objc_msgSend_Int64_IntPtr(smAppService, selector, url);

            return status is >= (long)ServiceManagement.LegacyServiceStatus.NotRegistered
                          and <= (long)ServiceManagement.LegacyServiceStatus.NotFound
                ? (ServiceManagement.LegacyServiceStatus)status
                : null;
        }
        finally {
            ObjCRuntime.objc_autoreleasePoolPop(pool);
        }
    }

    public static List<Dictionary<string, object?>>? GetSystemJobs() => GetJobs(SystemDomain.Value);

    public static List<Dictionary<string, object?>>? GetUserJobs() => GetJobs(UserDomain.Value);

    private static bool IsLoaded(nint domain, string label)
    {
        if (domain == nint.Zero || label.Length == 0) {
            return false;
        }

        using CFScope cfLabel = new(CFConvert.CreateString(label));

        try {
            using CFScope job = new(ServiceManagement.SMJobCopyDictionary(domain, cfLabel));

            return !job.IsNull;
        }
        catch (EntryPointNotFoundException) {
            return false;
        }
    }

    public static bool IsLoadedInSystemDomain(string label) => IsLoaded(SystemDomain.Value, label);

    public static bool IsLoadedInUserDomain(string label) => IsLoaded(UserDomain.Value, label);
}
#endif
