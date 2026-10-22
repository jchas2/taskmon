using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public sealed class Smc : IDisposable
{
    private const uint KernelIndexSmc = 2;
    private const byte ReadBytes      = 5;
    private const byte ReadKeyInfo    = 9;

    private static readonly uint FloatType = EncodeKey("flt ");

    private readonly uint connection;

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    private unsafe struct KeyData
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

    private Smc(uint connection) => this.connection = connection;

    private unsafe bool Call(ref KeyData input, out KeyData output)
    {
        output = default;
        nuint outputSize = (nuint)sizeof(KeyData);

        fixed (KeyData* inputPtr = &input)
        fixed (KeyData* outputPtr = &output) {
            return IOKit.IOConnectCallStructMethod(
                connection,
                KernelIndexSmc,
                inputPtr,
                (nuint)sizeof(KeyData),
                outputPtr,
                &outputSize) == 0 && outputPtr->Result == 0;
        }
    }

    public void Dispose() => IOKit.IOServiceClose(connection);

    public static uint EncodeKey(string key)
    {
        if (key.Length != 4) {
            throw new ArgumentException("SMC keys are four characters.", nameof(key));
        }

        return (uint)key[0] << 24 | (uint)key[1] << 16 | (uint)key[2] << 8 | key[3];
    }

    public static Smc? Open()
    {
        using IOObjectScope service = new(IOKit.IOServiceGetMatchingService(0, IOKit.IOServiceMatching("AppleSMC")));

        if (service.IsNull) {
            return null;
        }

        return IOKit.IOServiceOpen(service, MachTask.task_self_trap(), 0, out uint connection) == 0
            ? new Smc(connection)
            : null;
    }

    public unsafe float? ReadFloat(string key)
    {
        KeyData input = new() { Key = EncodeKey(key), Data8 = ReadKeyInfo };

        if (!Call(ref input, out KeyData info) || info.DataType != FloatType || info.DataSize != sizeof(float)) {
            return null;
        }

        input.DataSize = info.DataSize;
        input.DataType = info.DataType;
        input.Data8    = ReadBytes;

        if (!Call(ref input, out KeyData value)) {
            return null;
        }

        return BinaryPrimitives.ReadSingleLittleEndian(new ReadOnlySpan<byte>(value.Bytes, sizeof(float)));
    }
}
