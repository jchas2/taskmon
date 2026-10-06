using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace Task.Monitor.Cli.Utils;

public static class Win32StringHelper
{
    // Parse array of strings from one buffer separated by NULL and a double NULL to terminate.
    public static string[] ParseMultiSz(ReadOnlySpan<char> buffer)
    {
        List<string> values = new();

        while (!buffer.IsEmpty)
        {
            int terminator = buffer.IndexOf('\0');

            if (terminator < 0) {
                values.Add(buffer.ToString());
                break;
            }

            if (terminator == 0) {
                break;
            }

            values.Add(buffer[..terminator].ToString());
            buffer = buffer[(terminator + 1)..];
        }

        return values.ToArray();
    }

    // String from a fixed size wide char buffer, clamped on the first \0 or the end of the buffer
    // so an unterminated buffer can't be read past.
    public static string FromNullTerminated(ReadOnlySpan<char> buffer)
    {
        int terminator = buffer.IndexOf('\0');

        return terminator >= 0
            ? buffer[..terminator].ToString()
            : buffer.ToString();
    }

    // As FromNullTerminated, for a single byte (ANSI) buffer. Decoded as Latin1 which maps every
    // byte to a char, so vendor-supplied metadata with high-bit characters can't fail to decode.
    public static string FromNullTerminatedAnsi(ReadOnlySpan<byte> buffer)
    {
        int terminator = buffer.IndexOf((byte)0);

        if (terminator >= 0) {
            buffer = buffer[..terminator];
        }

        return Encoding.Latin1.GetString(buffer);
    }

    [SupportedOSPlatform("windows")]
    public static string HiveShortName(RegistryHive hive) => hive switch {
        RegistryHive.LocalMachine => "HKLM",
        RegistryHive.CurrentUser  => "HKCU",
                                _ => hive.ToString()
    };
}
