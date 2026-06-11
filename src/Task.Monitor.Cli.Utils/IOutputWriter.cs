namespace Task.Monitor.Cli.Utils;

public interface IOutputWriter
{
    void Write(string message);
    void WriteLine();
    void WriteLine(string message);
    void WriteLine(string format, params object?[] args);
}