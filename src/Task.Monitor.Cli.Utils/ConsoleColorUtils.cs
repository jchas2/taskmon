namespace Task.Monitor.Cli.Utils;

public static class ConsoleColorUtils
{
    private static Dictionary<string, ConsoleColor>? colourMap; 
    
    public static ConsoleColor FromName(string name, ConsoleColor defaultColour)
    {
        colourMap ??= GetConsoleColours();
        
        string key = GetKey(name);
        return colourMap.GetValueOrDefault(key, defaultColour);
    }

    private static Dictionary<string, ConsoleColor> GetConsoleColours()
    {
        var map = new Dictionary<string, ConsoleColor>();
        
        foreach (var color in Enum.GetValues<ConsoleColor>()) {
            string key = GetKey(color);
            map[key] = color;
        }

        return map;
    }

    private static string GetKey(ConsoleColor color) => color.ToString().ToLower();

    private static string GetKey(string name) => name.ToLower();
}
