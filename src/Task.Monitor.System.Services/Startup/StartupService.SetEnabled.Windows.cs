#if __WIN32__
using System.Buffers.Binary;
using System.Diagnostics;
using System.Security;
using Microsoft.Win32;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Startup;

#pragma warning disable CA1416 // Validate platform compatibility

public partial class StartupService
{
    private const int ApprovedStateLength = 12;

    private const uint ApprovedDefaultFlags = 0x02;
    private const uint ApprovedDisabledFlag = 0x01;

    private const int E_ACCESSDENIED = unchecked((int)0x80070005);
    private const int E_FILENOTFOUND = unchecked((int)0x80070002);
    private const int E_PATHNOTFOUND = unchecked((int)0x80070003);

    private partial StartupToggleResult ApplyEnabled(StartupEntry entry, bool enabled) => entry switch {
        { Source: StartupEntrySource.ScheduledTask } => SetScheduledTaskEnabled(entry, enabled),
        { Approval: { } approval }                   => WriteApprovedState(approval, enabled, DateTime.UtcNow),
                                                   _ => StartupToggleResult.NotSupported
    };

    internal static byte[] BuildApprovedState(ReadOnlySpan<byte> existing, bool enabled, DateTime nowUtc)
    {
        uint flags = existing.Length >= sizeof(uint)
            ? BinaryPrimitives.ReadUInt32LittleEndian(existing)
            : ApprovedDefaultFlags;

        byte[] state = new byte[ApprovedStateLength];

        BinaryPrimitives.WriteUInt32LittleEndian(
            state,
            enabled ? flags & ~ApprovedDisabledFlag : flags | ApprovedDisabledFlag);

        BinaryPrimitives.WriteInt64LittleEndian(
            state.AsSpan(sizeof(uint)),
            enabled ? 0 : nowUtc.ToFileTimeUtc());

        return state;
    }

    private static StartupToggleResult SetScheduledTaskEnabled(StartupEntry entry, bool enabled)
    {
        int hr = ScheduledTasks.SetEnabled(entry.Origin, entry.Name, enabled);

        if (hr < 0) {
            Trace.WriteLine($"{nameof(SetScheduledTaskEnabled)} {entry.Origin}\\{entry.Name}: 0x{hr:X8}");
        }

        return ToToggleResult(hr);
    }

    internal static StartupToggleResult ToToggleResult(int hResult) => hResult switch {
        >= 0                              => StartupToggleResult.Succeeded,
        E_ACCESSDENIED                    => StartupToggleResult.AccessDenied,
        E_FILENOTFOUND or E_PATHNOTFOUND  => StartupToggleResult.NotFound,
                                        _ => StartupToggleResult.Failed
    };

    private static StartupToggleResult WriteApprovedState(StartupApproval approval, bool enabled, DateTime nowUtc)
    {
        try {
            using RegistryKey baseKey     = RegistryKey.OpenBaseKey(approval.Hive, RegistryView.Registry64);
            using RegistryKey approvedKey = baseKey.CreateSubKey(approval.KeyPath, writable: true);

            byte[] existing = approvedKey.GetValue(approval.ValueName) as byte[] ?? [];

            approvedKey.SetValue(
                approval.ValueName,
                BuildApprovedState(existing, enabled, nowUtc),
                RegistryValueKind.Binary);

            return StartupToggleResult.Succeeded;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException) {
            return StartupToggleResult.AccessDenied;
        }
        catch (IOException ex) {
            Trace.WriteLine($"{nameof(WriteApprovedState)} {approval.KeyPath}\\{approval.ValueName}: {ex.Message}");
            return StartupToggleResult.Failed;
        }
    }
}

#pragma warning restore CA1416 // Validate platform compatibility
#endif
