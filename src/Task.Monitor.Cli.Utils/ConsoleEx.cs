namespace Task.Monitor.Cli.Utils;

public static class ConsoleEx 
{
    // Simple static class until C# supports static extension methods.
    private static bool isAltBufferActive = false;
    
    public static void SetAlternateScreenBuffer()
    {
        Console.Write("\x1b[?1049h\x1b[2J\x1b[H");
        Console.Out.Flush();
        isAltBufferActive = true;
    }

    public static void RestoreScreenBuffer()
    {
        if (!isAltBufferActive) {
            return;
        }

        Console.Write("\x1b[2J\x1b[?1049l");
        Console.Out.Flush();
        isAltBufferActive = false;
    }
}
