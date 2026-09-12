namespace Task.Monitor.System.Services.Disk;

// Several Win32 calls return a MULTI_SZ: NUL separated strings terminated by an empty one, so the
// buffer ends in a double NUL. GetVolumePathNamesForVolumeNameW is the one used here.
public static class MultiSzParser
{
    public static string[] Parse(ReadOnlySpan<char> buffer)
    {
        List<string> values = new();

        while (!buffer.IsEmpty)
        {
            int terminator = buffer.IndexOf('\0');

            // No terminator at all means the caller handed us an unterminated buffer. Take what
            // is there rather than reading past the end.
            if (terminator < 0) {
                values.Add(buffer.ToString());
                break;
            }

            // An empty string is the MULTI_SZ terminator.
            if (terminator == 0) {
                break;
            }

            values.Add(buffer[..terminator].ToString());
            buffer = buffer[(terminator + 1)..];
        }

        return values.ToArray();
    }
}
