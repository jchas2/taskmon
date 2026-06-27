namespace Task.Monitor.Cli.Utils;

public sealed class TerminalColourRestorer : IDisposable
{
    public void Dispose() => Console.Out.Write(AnsiConsoleStringExtensions.Reset);
}
