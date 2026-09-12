using System.Buffers.Binary;
using Task.Monitor.System.Services.Cpu;

namespace Task.Monitor.System.Services.Tests.Cpu;

public sealed class CpuTopologyParserTests
{
    private const uint RelationCache            = 2;
    private const uint RelationProcessorPackage = 3;

    // A SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX record: DWORD Relationship; DWORD Size; payload.
    // The payload beyond the fields the parser reads is left zeroed; only Size has to be honest.
    private static byte[] Record(uint relationship)
    {
        byte[] record = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0), relationship);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(4), (uint)record.Length);
        return record;
    }

    private static byte[] Cache(byte level, uint sizeInBytes)
    {
        byte[] record = Record(RelationCache);
        record[8] = level;                                                        // CACHE_RELATIONSHIP.Level
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(12), sizeInBytes);  // CACHE_RELATIONSHIP.CacheSize
        return record;
    }

    private static byte[] Concat(params byte[][] records)
    {
        byte[] result = new byte[records.Sum(record => record.Length)];
        int offset = 0;

        foreach (byte[] record in records) {
            record.CopyTo(result, offset);
            offset += record.Length;
        }

        return result;
    }

    [Fact]
    public void Should_Sum_Cache_Sizes_By_Level()
    {
        byte[] buffer = Concat(
            Cache(1, 32 * 1024),          // L1 instruction
            Cache(1, 32 * 1024),          // L1 data
            Cache(2, 512 * 1024),
            Cache(3, 8 * 1024 * 1024),
            Record(RelationProcessorPackage));

        CpuTopology topology = CpuTopologyParser.Parse(buffer);

        Assert.Equal(64u * 1024u, topology.L1CacheBytes);
        Assert.Equal(512u * 1024u, topology.L2CacheBytes);
        Assert.Equal(8u * 1024u * 1024u, topology.L3CacheBytes);
        Assert.Equal(1u, topology.SocketCount);
    }

    [Fact]
    public void Should_Count_One_Socket_Per_Package_Record() =>
        Assert.Equal(2u, CpuTopologyParser.Parse(
            Concat(Record(RelationProcessorPackage), Record(RelationProcessorPackage))).SocketCount);

    [Fact]
    public void Should_Fold_L4_Into_L3() =>
        Assert.Equal(3_000u, CpuTopologyParser.Parse(
            Concat(Cache(3, 1_000), Cache(4, 2_000))).L3CacheBytes);

    [Fact]
    public void Should_Stop_On_A_Zero_Size_Record()
    {
        // Relationship set, Size left at zero: a naive walk would never advance.
        byte[] record = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0), RelationProcessorPackage);

        Assert.Equal(0u, CpuTopologyParser.Parse(record).SocketCount);
    }

    [Fact]
    public void Should_Ignore_A_Trailing_Truncated_Record() =>
        // Four trailing bytes, fewer than a record header.
        Assert.Equal(1u, CpuTopologyParser.Parse(
            Concat(Record(RelationProcessorPackage), new byte[4])).SocketCount);

    [Fact]
    public void Should_Return_An_Empty_Topology_For_An_Empty_Buffer() =>
        Assert.Equal(default, CpuTopologyParser.Parse(ReadOnlySpan<byte>.Empty));
}
