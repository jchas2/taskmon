namespace Task.Monitor.System.Services.Power;

public sealed class PowerReading
{
    public PowerComponent Component { get; set; }

    // Empty for the CPU / system.
    public string ComponentId       { get; set; } = string.Empty;
    public string Rail              { get; set; } = string.Empty;
    public double Watts             { get; set; }
    public bool IsRated             { get; set; }
    public PowerSource Source       { get; set; }
}
