namespace Task.Monitor.Cli.Utils;

public sealed class OutputWriter : IOutputWriter
{
    private readonly TextWriter writer;

    private static IOutputWriter errorWriter = new OutputWriter(Console.Error);
    private static IOutputWriter outWriter = new OutputWriter(Console.Out);
    private static Lock lockObject = new ();
    
    public OutputWriter(TextWriter writer) => 
        this.writer = writer;
    
    public static IOutputWriter Error => errorWriter;

    public static IOutputWriter Out => outWriter;

    public static void SetErrorWriter(OutputWriter errorWriter)
    {
        lock (lockObject) {
            OutputWriter.errorWriter = errorWriter;
        }
    }

    public static void SetOutputWriter(IOutputWriter outWriter)
    {
        lock (lockObject) {
            OutputWriter.outWriter = outWriter;
        }
    }

    public void Write(string message)
    {
        lock (lockObject) {
            writer?.Write(message);
        }
    }

    public void WriteLine()
    {
        lock (lockObject) {
            writer?.WriteLine();
        }

    }

    public void WriteLine(string message)
    {
        lock (lockObject) {
            writer?.WriteLine(message);
        }
    }

    public void WriteLine(string format, params object?[] args)
    {
        lock (lockObject) {
            writer?.WriteLine(string.Format(format, args));
        }
    }
}
