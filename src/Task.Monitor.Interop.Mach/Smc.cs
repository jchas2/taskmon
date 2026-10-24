using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Mach;

public sealed class Smc : IDisposable
{
    private const uint KernelIndexSmc = 2;
    private const byte ReadBytes      = 5;
    private const byte ReadIndex      = 8;
    private const byte ReadKeyInfo    = 9;
    private const string KeyCountKey  = "#KEY";

    private readonly Dictionary<uint, (uint Size, uint Type)?> keyInfo = new();

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

    public static double? Decode(string dataType, ReadOnlySpan<byte> bytes) => dataType switch {
        "flt " when bytes.Length >= 4 => BinaryPrimitives.ReadSingleLittleEndian(bytes),
        "sp78" when bytes.Length >= 2 => BinaryPrimitives.ReadInt16BigEndian(bytes) / 256.0,
        "fpe2" when bytes.Length >= 2 => BinaryPrimitives.ReadUInt16BigEndian(bytes) / 4.0,
        "ui8 " when bytes.Length >= 1 => bytes[0],
        "ui16" when bytes.Length >= 2 => BinaryPrimitives.ReadUInt16BigEndian(bytes),
        "ui32" when bytes.Length >= 4 => BinaryPrimitives.ReadUInt32BigEndian(bytes),
        _ => null
    };

    public static string DecodeKey(uint key) =>
        string.Create(4, key, (chars, value) => {
            chars[0] = (char)(byte)(value >> 24);
            chars[1] = (char)(byte)(value >> 16);
            chars[2] = (char)(byte)(value >> 8);
            chars[3] = (char)(byte)value;
        });

    public void Dispose() => IOKit.IOServiceClose(connection);

    public static uint EncodeKey(string key)
    {
        if (key.Length != 4) {
            throw new ArgumentException("SMC keys are four characters.", nameof(key));
        }

        return (uint)key[0] << 24 | (uint)key[1] << 16 | (uint)key[2] << 8 | key[3];
    }

    private (uint Size, uint Type)? GetKeyInfo(uint key)
    {
        if (keyInfo.TryGetValue(key, out (uint Size, uint Type)? cached)) {
            return cached;
        }

        KeyData input = new() { Key = key, Data8 = ReadKeyInfo };

        (uint Size, uint Type)? info = Call(ref input, out KeyData output)
            ? (output.DataSize, output.DataType)
            : null;

        keyInfo[key] = info;
        return info;
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

    public List<string> ReadKeys()
    {
        List<string> keys = new();

        if (ReadValue(KeyCountKey) is not { } count) {
            return keys;
        }

        for (uint i = 0; i < (uint)count; i++) {
            KeyData input = new() { Data8 = ReadIndex, Data32 = i };

            if (Call(ref input, out KeyData output)) {
                keys.Add(DecodeKey(output.Key));
            }
        }

        return keys;
    }

    public unsafe double? ReadValue(string key)
    {
        uint code = EncodeKey(key);

        if (GetKeyInfo(code) is not { } info || info.Size > 32) {
            return null;
        }

        KeyData input = new() { Key = code, DataSize = info.Size, DataType = info.Type, Data8 = ReadBytes };

        if (!Call(ref input, out KeyData value)) {
            return null;
        }

        return Decode(DecodeKey(info.Type), new ReadOnlySpan<byte>(value.Bytes, (int)info.Size));
    }
}
