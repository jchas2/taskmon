using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Task.Monitor.Cli.Utils;

public static class InteropHelper
{
    public static void MarshalRelease(ref nint pointer)
    {
        if (pointer != nint.Zero) {
            Marshal.Release(pointer);
            pointer = nint.Zero;
        }
    }

    public static unsafe string? TryMarshalPtrToStringAnsi(nint pointer, int maxLength)
    {
        if (pointer == nint.Zero || maxLength <= 0) {
            return string.Empty;
        }

        // Prevent access violation by clamping on sentinel \0 within\to maxLength.
        int length = new ReadOnlySpan<byte>((void*)pointer, maxLength).IndexOf((byte)0);

        if (length < 0) {
            length = maxLength;
        }

        try {
            return length == 0
                ? string.Empty
                : Marshal.PtrToStringAnsi(pointer, length);
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
            return string.Empty;
        }
    }

    // Field reads at a byte offset into a native structure. Unaligned and little endian, as
    // offset-based layouts (e.g. IP_ADAPTER_ADDRESSES, MIB_IF_ROW2) don't guarantee alignment.
    public static unsafe byte* ReadPointer(byte* structure, int offset) =>
        (byte*)Unsafe.ReadUnaligned<nint>(structure + offset);

    public static unsafe uint ReadUInt32(byte* structure, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(structure + offset, sizeof(uint)));

    public static unsafe ulong ReadUInt64(byte* structure, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(structure + offset, sizeof(ulong)));

    public static string TryMarshalPtrToStringBSTR(nint pointer)
    {
        // PtrToStringBSTR can throw on a pointer to 0 (null); however this is a valid
        // empty BSTR.
        if (pointer == nint.Zero) {
            return string.Empty;
        }

        try {
            return Marshal.PtrToStringBSTR(pointer);
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
            return string.Empty;
        }
    }
}
