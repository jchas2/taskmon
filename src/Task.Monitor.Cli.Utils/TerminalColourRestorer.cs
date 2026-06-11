namespace Task.Monitor.Cli.Utils;

public sealed class TerminalColourRestorer : IDisposable
{
    private readonly ConsoleColor cachedBackgroundColour;
    private readonly ConsoleColor cachedForegroundColour;

    public TerminalColourRestorer()
    {
        cachedBackgroundColour = Console.BackgroundColor;
        cachedForegroundColour = Console.ForegroundColor;
    }
    
    public void Dispose()
    {
        Console.BackgroundColor = cachedBackgroundColour;
        Console.ForegroundColor = cachedForegroundColour;
    }
}