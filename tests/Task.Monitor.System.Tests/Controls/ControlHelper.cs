namespace Task.Monitor.System.Tests.Controls;

public static class ControlHelper
{
    public static ConsoleKeyInfo GetConsoleKeyInfo(ConsoleKey key) => 
        new ConsoleKeyInfo(
            (char)key,
            key,
            shift: false,
            alt: false,
            control: false);
}
