namespace Task.Monitor.System.Services.Drivers;

// A kernel-mode or file-system driver registered with the Service Control Manager on Windows, or a
// kernel extension on macOS.
public sealed class DriverInfo
{
    public string          Name             { get; set; } = string.Empty;
    public string          DisplayName      { get; set; } = string.Empty;
    public bool            DelayedAutoStart { get; set; }
    public string?         ImagePath        { get; set; }
    public string?         Version          { get; set; }
    public string?         Publisher        { get; set; }
    public DriverStatus    Status           { get; set; }
    public DriverStartType StartType        { get; set; }
}
