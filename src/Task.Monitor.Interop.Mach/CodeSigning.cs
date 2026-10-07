using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class CodeSigning
{
    private const int  errSecSuccess = 0;
    private const uint kSecCSDefaultFlags = 0;
    private const uint kSecCSSigningInformation = 1 << 1;

    private static readonly Lazy<nint> CertificatesKey = new(() =>
        DyLib.ReadExportedPointer(Libraries.Security, "kSecCodeInfoCertificates"));

    [DllImport(Libraries.Security)]
    private static extern int SecStaticCodeCreateWithPath(nint path, uint flags, out nint staticCode);

    [DllImport(Libraries.Security)]
    private static extern int SecCodeCopySigningInformation(nint code, uint flags, out nint information);

    [DllImport(Libraries.Security)]
    private static extern nint SecCertificateCopySubjectSummary(nint certificate);

    public static string? GetSigningCertificateSummary(string path)
    {
        nint certificatesKey = CertificatesKey.Value;

        if (certificatesKey == nint.Zero) {
            return null;
        }

        using CFScope url = new(CoreFoundation.CFURLCreateFromPath(path, Directory.Exists(path)));

        if (url.IsNull) {
            return null;
        }

        int status = SecStaticCodeCreateWithPath(url, kSecCSDefaultFlags, out nint staticCodeRef);
        using CFScope staticCode = new(staticCodeRef);

        if (status != errSecSuccess) {
            return null;
        }

        status = SecCodeCopySigningInformation(staticCode, kSecCSSigningInformation, out nint informationRef);
        using CFScope information = new(informationRef);

        if (status != errSecSuccess) {
            return null;
        }

        nint certificates = CoreFoundation.CFDictionaryGetValue(information, certificatesKey);

        if (certificates == nint.Zero || CoreFoundation.CFArrayGetCount(certificates) == 0) {
            return null;
        }

        using CFScope summary = new(SecCertificateCopySubjectSummary(CoreFoundation.CFArrayGetValueAtIndex(certificates, 0)));

        return CoreFoundation.GetString(summary);
    }
}
