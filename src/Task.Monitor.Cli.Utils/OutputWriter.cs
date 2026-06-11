namespace Task.Monitor.Cli.Utils;

public sealed class OutputWriter : IOutputWriter
{
    private readonly TextWriter writer;

    private static readonly OutputWriter errorWriter = new OutputWriter(Console.Error);
    private static readonly OutputWriter outWriter = new OutputWriter(Console.Out);
    
    public OutputWriter(TextWriter writer) => 
        this.writer = writer;
    
    public static OutputWriter Error => errorWriter;

    public static OutputWriter Out => outWriter;
    
    public void Write(string message) =>
        writer?.Write(message);

    public void WriteLine() =>
        writer?.WriteLine();

    public void WriteLine(string message) =>
        writer?.WriteLine(message);
    
    public void WriteLine(string format, params object?[] args) =>
        writer?.WriteLine(string.Format(format, args));
}
