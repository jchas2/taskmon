#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public static class CodeSignature
{
    private static readonly Lazy<nint> CertificatesKey = new(() =>
        NativeExports.ReadPointer(Libraries.Security, CodeSigning.kSecCodeInfoCertificates));

    public static string? GetSigningCertificateSummary(string path)
    {
        nint certificatesKey = CertificatesKey.Value;

        if (certificatesKey == nint.Zero) {
            return null;
        }

        using CFScope url = new(CFConvert.CreateFileUrl(path, Directory.Exists(path)));

        if (url.IsNull) {
            return null;
        }

        int status = CodeSigning.SecStaticCodeCreateWithPath(url, CodeSigning.kSecCSDefaultFlags, out nint staticCodeRef);

        using CFScope staticCode = new(staticCodeRef);

        if (status != CodeSigning.errSecSuccess) {
            return null;
        }

        status = CodeSigning.SecCodeCopySigningInformation(staticCode, CodeSigning.kSecCSSigningInformation, out nint informationRef);

        using CFScope information = new(informationRef);

        if (status != CodeSigning.errSecSuccess) {
            return null;
        }

        nint certificates = CoreFoundation.CFDictionaryGetValue(information, certificatesKey);

        if (certificates == nint.Zero || CoreFoundation.CFArrayGetCount(certificates) == 0) {
            return null;
        }

        using CFScope summary = new(CodeSigning.SecCertificateCopySubjectSummary(CoreFoundation.CFArrayGetValueAtIndex(certificates, 0)));

        return CFConvert.GetString(summary);
    }
}
#endif
