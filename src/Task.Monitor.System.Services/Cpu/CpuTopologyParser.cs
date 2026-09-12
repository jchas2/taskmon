using System.Buffers.Binary;

namespace Task.Monitor.System.Services.Cpu;

public readonly record struct CpuTopology(
    uint SocketCount,
    ulong L1CacheBytes,
    ulong L2CacheBytes,
    ulong L3CacheBytes);

// Decodes the SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX record stream returned by
// GetLogicalProcessorInformationEx. Kept free of P/Invoke so the buffer walk can be unit tested
// from a captured payload, in the same spirit as MemoryDeviceParser.
public static class CpuTopologyParser
{
    // Common record header: LOGICAL_PROCESSOR_RELATIONSHIP Relationship; DWORD Size.
    private const int HeaderSize          = 8;
    private const int RelationshipOffset  = 0;
    private const int SizeOffset          = 4;

    // CACHE_RELATIONSHIP begins at the end of the header: BYTE Level; BYTE Associativity;
    // WORD LineSize; DWORD CacheSize; PROCESSOR_CACHE_TYPE Type; ...
    private const int CacheLevelOffset    = HeaderSize + 0;
    private const int CacheSizeOffset     = HeaderSize + 4;

    private const uint RelationCache            = 2;
    private const uint RelationProcessorPackage = 3;

    public static CpuTopology Parse(ReadOnlySpan<byte> buffer)
    {
        uint sockets = 0;
        ulong l1 = 0;
        ulong l2 = 0;
        ulong l3 = 0;

        int cursor = 0;

        while (cursor + HeaderSize <= buffer.Length)
        {
            uint relationship = BinaryPrimitives.ReadUInt32LittleEndian(buffer[(cursor + RelationshipOffset)..]);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(buffer[(cursor + SizeOffset)..]);

            // A zero or overrunning Size would spin forever or read out of bounds on a corrupt buffer.
            if (size < HeaderSize || cursor + (long)size > buffer.Length) {
                break;
            }

            switch (relationship)
            {
                case RelationProcessorPackage:
                    sockets++;
                    break;

                case RelationCache when cursor + CacheSizeOffset + sizeof(uint) <= cursor + size:
                    byte level = buffer[cursor + CacheLevelOffset];
                    uint cacheSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer[(cursor + CacheSizeOffset)..]);

                    switch (level)
                    {
                        case 1:      l1 += cacheSize; break;
                        case 2:      l2 += cacheSize; break;
                        case >= 3:   l3 += cacheSize; break; // fold the rare L4 into L3
                    }

                    break;
            }

            cursor += (int)size;
        }

        return new CpuTopology(sockets, l1, l2, l3);
    }
}
