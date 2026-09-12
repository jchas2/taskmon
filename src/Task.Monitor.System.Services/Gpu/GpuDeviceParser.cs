using System.Globalization;

namespace Task.Monitor.System.Services.Gpu;

// Pure decode helpers for the GPU subsystem: a PCI id to a vendor name, a DXGI adapter
// description to an adapter type, and a \GPU Engine(*) / \GPU Adapter Memory(*) counter instance
// name to its pid, engine key and adapter LUID. No interop dependency, so it is unit tested the
// same way DiskDeviceParser and NetworkDeviceParser are.
public static class GpuDeviceParser
{
    public const string NotAvailable = "N/A";

    // DXGI_ADAPTER_FLAG_SOFTWARE. Duplicated here so the parser stays free of an interop reference.
    private const uint DxgiAdapterFlagSoftware = 2;

    // An adapter reporting less dedicated VRAM than this is taken to be an integrated GPU carving a
    // slice of system memory rather than a discrete card. A heuristic: no DXGI field states it.
    private const long IntegratedVramThreshold = 512L * 1024 * 1024;

    public static string DecodeVendor(uint vendorId) => vendorId switch
    {
        0x10DE => "NVIDIA",
        0x1002 => "AMD",
        0x1022 => "AMD",
        0x8086 => "Intel",
        0x1414 => "Microsoft",
        0x15AD => "VMware",
        0x1AF4 => "Red Hat",
        0x80EE => "VirtualBox",
        0x1234 => "QEMU",
        0x1D0F => "Amazon",
        _      => NotAvailable,
    };

    public static string DecodeAdapterType(uint dxgiFlags, uint vendorId, long dedicatedVideoMemory)
    {
        if ((dxgiFlags & DxgiAdapterFlagSoftware) != 0) {
            return "Software";
        }

        if (IsVirtualVendor(vendorId)) {
            return "Virtual";
        }

        return dedicatedVideoMemory >= IntegratedVramThreshold ? "Discrete" : "Integrated";
    }

    private static bool IsVirtualVendor(uint vendorId) => vendorId switch
    {
        0x1414 or 0x15AD or 0x1AF4 or 0x80EE or 0x1234 or 0x1D0F => true,
        _ => false,
    };

    // Format example: pid_1234_luid_0x00000000_0x0000ABCD_phys_0_eng_3_engtype_3D
    public static int ParsePidFromInstance(string instanceName)
    {
        const string pidPrefix = "pid_";

        if (!instanceName.StartsWith(pidPrefix, StringComparison.OrdinalIgnoreCase)) {
            return -1;
        }

        int separatorIndex = instanceName.IndexOf('_', pidPrefix.Length);

        if (separatorIndex == -1) {
            return -1;
        }

        ReadOnlySpan<char> pidSpan = instanceName.AsSpan(
            pidPrefix.Length,
            separatorIndex - pidPrefix.Length);

        return int.TryParse(pidSpan, out int pid)
            ? pid
            : -1;
    }

    // Everything after the pid identifies one engine on one adapter, and is the key the processes
    // sharing that engine have in common.
    //
    // Every engtype counts. The suffix is not limited to the DXGK_ENGINE_TYPE enum names: drivers
    // name their own nodes, so an NVIDIA card also reports engtype_Compute_0, engtype_OFA_0,
    // engtype_VR and engtype_Security.
    public static string? ParseEngineFromInstance(string instanceName)
    {
        const string pidPrefix = "pid_";

        if (!instanceName.StartsWith(pidPrefix, StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        int separatorIndex = instanceName.IndexOf('_', pidPrefix.Length);

        if (separatorIndex == -1 || separatorIndex + 1 >= instanceName.Length) {
            return null;
        }

        return instanceName[(separatorIndex + 1)..];
    }

    // The luid_0x{HighPart}_0x{LowPart} token carried by a \GPU Engine(*) or \GPU Adapter
    // Memory(*) counter instance, packed the same way DXGI stores it in
    // DXGI_ADAPTER_DESC1.AdapterLuid so the two compare equal.
    public static bool TryParseAdapterLuid(string instanceName, out long luid)
    {
        luid = 0;

        const string luidToken = "luid_";
        int tokenIndex = instanceName.IndexOf(luidToken, StringComparison.OrdinalIgnoreCase);

        if (tokenIndex < 0) {
            return false;
        }

        ReadOnlySpan<char> span = instanceName.AsSpan(tokenIndex + luidToken.Length);

        if (!TryReadHexGroup(ref span, out uint highPart)) {
            return false;
        }

        if (span.IsEmpty || span[0] != '_') {
            return false;
        }

        span = span[1..];

        if (!TryReadHexGroup(ref span, out uint lowPart)) {
            return false;
        }

        luid = ((long)highPart << 32) | lowPart;
        return true;
    }

    // VEN_xxxx and DEV_xxxx out of a Registry MatchingDeviceId such as
    // "pci\ven_10de&dev_2482&subsys_40BF1458&rev_a1".
    public static bool TryParsePciIds(string? matchingDeviceId, out uint vendorId, out uint deviceId)
    {
        vendorId = 0;
        deviceId = 0;

        if (string.IsNullOrEmpty(matchingDeviceId)) {
            return false;
        }

        return TryReadHexAfter(matchingDeviceId, "VEN_", out vendorId) &&
               TryReadHexAfter(matchingDeviceId, "DEV_", out deviceId);
    }

    private static bool TryReadHexAfter(string text, string token, out uint value)
    {
        value = 0;

        int tokenIndex = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);

        if (tokenIndex < 0) {
            return false;
        }

        int start = tokenIndex + token.Length;
        int end = start;

        while (end < text.Length && Uri.IsHexDigit(text[end])) {
            end++;
        }

        return end > start &&
               uint.TryParse(text.AsSpan(start, end - start), NumberStyles.HexNumber, null, out value);
    }

    private static bool TryReadHexGroup(ref ReadOnlySpan<char> span, out uint value)
    {
        value = 0;

        if (span.Length < 3 || span[0] != '0' || (span[1] != 'x' && span[1] != 'X')) {
            return false;
        }

        ReadOnlySpan<char> digits = span[2..];
        int end = 0;

        while (end < digits.Length && Uri.IsHexDigit(digits[end])) {
            end++;
        }

        if (end == 0 ||
            !uint.TryParse(digits[..end], NumberStyles.HexNumber, null, out value)) {
            return false;
        }

        span = digits[end..];
        return true;
    }
}
