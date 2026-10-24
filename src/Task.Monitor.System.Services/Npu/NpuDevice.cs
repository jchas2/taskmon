namespace Task.Monitor.System.Services.Npu;

public sealed class NpuDevice
{
    public int    Index        { get; set; }
    public string Description  { get; set; } = string.Empty;
    public string Vendor       { get; set; } = string.Empty;
    public int    Cores        { get; set; }
    public string Architecture { get; set; } = string.Empty;
    public string Version      { get; set; } = string.Empty;
}
