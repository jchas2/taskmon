using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class CodeSigning
{
    public const int  errSecSuccess            = 0;
    public const uint kSecCSDefaultFlags       = 0;
    public const uint kSecCSSigningInformation = 1 << 1;

    public const string kSecCodeInfoCertificates = "kSecCodeInfoCertificates";

    [DllImport(Libraries.Security)]
    public static extern nint SecCertificateCopySubjectSummary(nint certificate);

    [DllImport(Libraries.Security)]
    public static extern int SecCodeCopySigningInformation(nint code, uint flags, out nint information);

    [DllImport(Libraries.Security)]
    public static extern int SecStaticCodeCreateWithPath(nint path, uint flags, out nint staticCode);
}
