using System.Diagnostics;

namespace Task.Monitor.Cli.Utils;

public class FormattedTextWriterTraceListener(string fileName) : TextWriterTraceListener(fileName)
{
    public override void Write(string? message)
    {
        string formattedMessage = FormatMessage(message);
        base.Write(formattedMessage);
    }

    public override void WriteLine(string? message)
    {
        string formattedMessage = FormatMessage(message);
        base.WriteLine(formattedMessage);
    }

    public static void Initialise()
    {
        string fileName = UseNextFileName();
        FormattedTextWriterTraceListener traceListener = new(fileName);
        
        Trace.Listeners.Add(traceListener);
        Trace.AutoFlush = true;
    }
    
    public static string UseNextFileName(string prefix = "debug", string extension = "txt")
    {
        // Use a timestamp format that sorts chronologically: "yyyyMMdd_HHmmss_fff".
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        return $"{prefix}_{timestamp}.{extension}";
    }

    private string FormatMessage(string? message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        return $"[{timestamp}] [Thread-{Thread.CurrentThread.ManagedThreadId}] {message}";
    }
}
