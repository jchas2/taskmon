using System.Collections.Immutable;

namespace Task.Monitor.System.Services.Cpu;

public sealed class CpuInfo
{
    public readonly record struct CpuCoreMetric(string Name, Double Value);

    public CpuSpecs Specs     { get; set; } = new();
    public CpuMetrics Metrics { get; set; } = new();
    
    public ImmutableArray<CpuCoreMetric> CoreMetrics { get; set; } = ImmutableArray<CpuCoreMetric>.Empty;
}
