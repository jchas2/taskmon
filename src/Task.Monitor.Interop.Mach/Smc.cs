using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public static class Smc
{
    public const string ServiceName = "AppleSMC";

    public const uint KernelIndexSmc = 2;
    public const byte ReadBytes      = 5;
    public const byte ReadIndex      = 8;
    public const byte ReadKeyInfo    = 9;

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public unsafe struct KeyData
    {
        [FieldOffset(0)]  public uint Key;
        [FieldOffset(28)] public uint DataSize;
        [FieldOffset(32)] public uint DataType;
        [FieldOffset(36)] public byte DataAttributes;
        [FieldOffset(40)] public byte Result;
        [FieldOffset(41)] public byte Status;
        [FieldOffset(42)] public byte Data8;
        [FieldOffset(44)] public uint Data32;
        [FieldOffset(48)] public fixed byte Bytes[32];
    }
}
