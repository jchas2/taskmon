namespace Task.Monitor.System.Services.Disk;

public static class MultiSzParser
{
    // Parse array of strings from one buffer separated by NULL and a double NULL to terminate.
    public static string[] Parse(ReadOnlySpan<char> buffer)
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
}
