namespace Task.Monitor.System.Services.LaunchdServices;

public sealed class LaunchdServiceInfo
{
    public string               Label          { get; set; } = string.Empty;
    public LaunchdDomain        Domain         { get; set; }
    public string               Group          { get; set; } = string.Empty;
    public LaunchdServiceStatus Status         { get; set; }
    public int?                 Pid            { get; set; }
    public LaunchdStartType     StartType      { get; set; }
    public bool                 Disabled       { get; set; }
    public string?              ProgramPath    { get; set; }
    public string?              Arguments      { get; set; }
    public string?              Publisher      { get; set; }
    public int?                 LastExitStatus { get; set; }
    public string?              PlistPath      { get; set; }
}
