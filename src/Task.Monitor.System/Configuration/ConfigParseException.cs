namespace Task.Monitor.System.Configuration;

public class ConfigParseException : Exception
{
    public ConfigParseException() { }
    
    public ConfigParseException(string message) : base(message) { }

    public ConfigParseException(string message, Exception innerException) : base(message, innerException) { }
    
    
}