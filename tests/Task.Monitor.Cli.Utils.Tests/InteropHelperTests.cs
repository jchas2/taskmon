using System.Buffers.Binary;

namespace Task.Monitor.Cli.Utils.Tests;

public sealed class InteropHelperTests
{
    [Fact]
    public unsafe void ReadUInt32_Should_Read_At_An_Unaligned_Offset()
    {
        byte* structure = stackalloc byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(structure + 3, sizeof(uint)), 0xDEADBEEF);

        Assert.Equal(0xDEADBEEFu, InteropHelper.ReadUInt32(structure, 3));
    }

    [Fact]
    public unsafe void ReadUInt64_Should_Read_At_An_Unaligned_Offset()
    {
        byte* structure = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(structure + 5, sizeof(ulong)), 0x0123456789ABCDEF);

        Assert.Equal(0x0123456789ABCDEFul, InteropHelper.ReadUInt64(structure, 5));
    }

    [Fact]
    public unsafe void ReadPointer_Should_Follow_A_Linked_Structure()
    {
        // The IP_ADAPTER_ADDRESSES style "Next" pointer chain.
        byte* first = stackalloc byte[32];
        byte* second = stackalloc byte[32];
        const int nextOffset = 8;

        *(nint*)(first + nextOffset) = (nint)second;
        *(nint*)(second + nextOffset) = nint.Zero;

        Assert.True(InteropHelper.ReadPointer(first, nextOffset) == second);
        Assert.True(InteropHelper.ReadPointer(second, nextOffset) == null);
    }
}
