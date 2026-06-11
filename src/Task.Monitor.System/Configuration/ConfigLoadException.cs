namespace Task.Monitor.System.Configuration;

public class ConfigLoadException : Exception
{
    public ConfigLoadException() { }
    
    public ConfigLoadException(string message) : base(message) { }

    public ConfigLoadException(string message, Exception innerException) : base(message, innerException) { }
}
