namespace Task.Monitor.System.Services.Process;

public sealed class WindowsServiceInfo
{
    public string                  ServiceName { get; set; } = string.Empty;
    public string                  DisplayName { get; set; } = string.Empty;
    public string?                 Description { get; set; }
    public WindowsServiceStatus    Status { get; set; }
    public WindowsServiceStartType StartType { get; set; }
    public bool                    DelayedAutoStart { get; set; }
    public string?                 LogOnAs { get; set; }
}
