using System.Diagnostics;
using System.Text;

namespace Task.Monitor.Cli.Utils;

public class FormattedTextWriterTraceListener : TextWriterTraceListener
{
    private readonly long maxBytes;
    private readonly int maxFiles;
    private readonly string prefix;
    private readonly string extension;
    private readonly string directory;
    private readonly Lock rollLock = new();
    private long bytesWritten;

    public FormattedTextWriterTraceListener(
        string fileName,
        long maxBytes,
        int maxFiles,
        string prefix,
        string extension)
        : base(fileName)
    {
        this.maxBytes = maxBytes;
        this.maxFiles = maxFiles;
        this.prefix = prefix;
        this.extension = extension;

        directory = Path.GetDirectoryName(Path.GetFullPath(fileName)) ?? ".";
    }

    public override void Write(string? message) => 
        WriteInternal(FormatMessage(message), newLine: false);

    public override void WriteLine(string? message) => 
        WriteInternal(FormatMessage(message), newLine: true);

    private void WriteInternal(string formatted, bool newLine)
    {
        lock (rollLock) {
            long length = Encoding.UTF8.GetByteCount(formatted) + (newLine ? Environment.NewLine.Length : 0);

            if (bytesWritten > 0 && bytesWritten + length > maxBytes) {
                RollFile();
            }

            if (newLine) {
                base.WriteLine(formatted);
            }
            else {
                base.Write(formatted);
            }
            
            bytesWritten += length;
        }
    }

    private void RollFile()
    {
        Writer?.Flush();
        Writer?.Dispose();

        string next = Path.Combine(directory, UseNextFileName(prefix, extension));
        
        Writer = new StreamWriter(next) {
            AutoFlush = true
        };
        
        bytesWritten = 0;
        PruneOldLogs();
    }

    private void PruneOldLogs()
    {
        try {
            string[] segments = Directory.GetFiles(directory, $"{prefix}_*.{extension}");

            if (segments.Length <= maxFiles) {
                return;
            }

            Array.Sort(segments, StringComparer.Ordinal);

            for (int i = 0; i < segments.Length - maxFiles; i++) {
                try {
                    File.Delete(segments[i]);
                }
                catch (Exception ex) {
                    Debug.Fail($"Failed PruneOldLogs: {ex}");
                }
            }
        }
        catch (Exception ex) {
            Debug.Fail($"Failed PruneOldLogs: {ex}");
        }
    }

    public static void Initialise(string directory, long maxBytes, int maxFiles, string prefix = "debug", string extension = "log")
    {
        if (!Directory.Exists(directory)) {
            throw new InvalidOperationException();
        }
        
        string fileName = Path.Combine(directory, UseNextFileName(prefix, extension));
        FormattedTextWriterTraceListener traceListener = new(fileName, maxBytes, maxFiles, prefix, extension);

        Trace.Listeners.Add(traceListener);
        Trace.AutoFlush = true;
    }

    private static string UseNextFileName(string prefix = "debug", string extension = "log") =>
        $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.{extension}";

    private string FormatMessage(string? message) =>
        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{Thread.CurrentThread.ManagedThreadId}] : {message}";
}
