#if __APPLE__
using System.Buffers.Binary;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Native;

public sealed class SmcConnection : IDisposable
{
    private const string KeyCountKey = "#KEY";

    private readonly Dictionary<uint, (uint Size, uint Type)?> keyInfo = new();

    private readonly uint connection;

    private SmcConnection(uint connection) => this.connection = connection;

    private unsafe bool Call(ref Smc.KeyData input, out Smc.KeyData output)
    {
        output = default;
        nuint outputSize = (nuint)sizeof(Smc.KeyData);

        fixed (Smc.KeyData* inputPtr = &input)
        fixed (Smc.KeyData* outputPtr = &output) {
            return IOKit.IOConnectCallStructMethod(
                connection,
                Smc.KernelIndexSmc,
                inputPtr,
                (nuint)sizeof(Smc.KeyData),
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

        Smc.KeyData input = new() { Key = key, Data8 = Smc.ReadKeyInfo };

        (uint Size, uint Type)? info = Call(ref input, out Smc.KeyData output)
            ? (output.DataSize, output.DataType)
            : null;

        keyInfo[key] = info;
        return info;
    }

    public static SmcConnection? Open() => Open(out _);

    public static SmcConnection? Open(out string diagnostic)
    {
        using IOObjectScope service = new(IOKit.IOServiceGetMatchingService(0, IOKit.IOServiceMatching(Smc.ServiceName)));

        if (service.IsNull) {
            diagnostic = $"NOT FOUND, no {Smc.ServiceName} service";
            return null;
        }

        int result = IOKit.IOServiceOpen(service, MachTask.task_self_trap(), 0, out uint connection);

        if (result != 0) {
            diagnostic = $"FAILED, IOServiceOpen {Smc.ServiceName} {ErrorCodes.DescribeKernReturn(result)}";
            return null;
        }

        diagnostic = "OK";
        return new SmcConnection(connection);
    }

    public List<string> ReadKeys()
    {
        List<string> keys = new();

        if (ReadValue(KeyCountKey) is not { } count) {
            return keys;
        }

        for (uint i = 0; i < (uint)count; i++) {
            Smc.KeyData input = new() { Data8 = Smc.ReadIndex, Data32 = i };

            if (Call(ref input, out Smc.KeyData output)) {
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

        Smc.KeyData input = new() { Key = code, DataSize = info.Size, DataType = info.Type, Data8 = Smc.ReadBytes };

        if (!Call(ref input, out Smc.KeyData value)) {
            return null;
        }

        return Decode(DecodeKey(info.Type), new ReadOnlySpan<byte>(value.Bytes, (int)info.Size));
    }
}
#endif
