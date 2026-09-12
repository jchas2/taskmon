using System.Buffers.Binary;

namespace Task.Monitor.System.Services.Startup;

// Parses the binary value Explorer writes under
//   HK{CU,LM}\...\CurrentVersion\Explorer\StartupApproved\{Run, Run32, StartupFolder}
// one per startup entry, to record whether the user has disabled it from Task Manager.
//
// Layout: a little-endian uint of status flags, then an 8-byte FILETIME of when the entry was
// last disabled (zero while enabled). Anything past the first 12 bytes carries nothing we read.
public static class StartupApprovedState
{
    // FILETIME that maps to DateTime.MaxValue; a larger value in a corrupt blob would throw.
    private static readonly long MaxFileTime = DateTime.MaxValue.ToFileTimeUtc();

    public static (StartupEntryState State, DateTime? DisabledOnUtc) Parse(ReadOnlySpan<byte> blob)
    {
        if (blob.Length < 12) {
            return (StartupEntryState.Unknown, null);
        }

        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(blob);

        // Bit 0 set marks the entry disabled. Enabled values seen in the wild are 0x02 and 0x06;
        // disabled are 0x03 and 0x07.
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
