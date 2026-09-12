using System.Collections.Immutable;

namespace Task.Monitor.System.Services.Memory;

public sealed class MemorySpecs
{
    public List<MemoryDevice> Devices { get; set; } = new();
}
