#if __WIN32__
using System.Buffers.Binary;

namespace Task.Monitor.System.Services.Startup;

// Parses the binary value Explorer writes under
//   HK{CU,LM}\...\CurrentVersion\Explorer\StartupApproved\{Run, Run32, StartupFolder}
public static class StartupApprovedState
{
    private static readonly long MaxFileTime = DateTime.MaxValue.ToFileTimeUtc();

    public static (StartupEntryState State, DateTime? DisabledOnUtc) Parse(ReadOnlySpan<byte> blob)
    {
        if (blob.Length < 12) {
            return (StartupEntryState.Unknown, null);
        }

        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(blob);
        
        if ((flags & 0x1) == 0) {
            return (StartupEntryState.Enabled, null);
        }

        long fileTime = BinaryPrimitives.ReadInt64LittleEndian(blob[4..]);

        DateTime? disabledOn = fileTime > 0 && fileTime <= MaxFileTime
            ? DateTime.FromFileTimeUtc(fileTime)
            : null;

        return (StartupEntryState.Disabled, disabledOn);
    }
}
#endif
