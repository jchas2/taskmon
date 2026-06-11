using Task.Monitor.Process;
using Task.Monitor.System;

namespace Task.Monitor.Tests.Process;

public sealed class ProcessorEventArgsTests
{
[Fact]                                                                                                                                                                                 
    public void Constructor_Initializes_ProcessInfos_Field()                                                                                                                               
    {                                                                                                                                                                                      
        List<ProcessorInfo> processInfos = new() {                                                                                                                                         
            new ProcessorInfo { Pid = 1234, ProcessName = "test.exe" }                                                                                                                     
        };
        
        SystemStatistics stats = new() { ProcessCount = 1 };                                                                                                                               
                                                                                                                                                                                         
        ProcessorEventArgs args = new(processInfos, stats);                                                                                                                                
                                                                                                                                                                                         
        Assert.NotNull(args.ProcessInfos);                                                                                                                                                 
        Assert.Same(processInfos, args.ProcessInfos);                                                                                                                                      
    }                                                                                                                                                                                      
                                                                                                                                                                                         
    [Fact]                                                                                                                                                                                 
    public void Constructor_Initializes_SystemStatistics_Field()                                                                                                                           
    {                                                                                                                                                                                      
        List<ProcessorInfo> processInfos = new();                                                                                                                                          
        SystemStatistics stats = new() {                                                                                                                                                   
            TotalPhysical = 1024 * 1024 * 1024,                                                                                                                                            
            ProcessCount = 5,                                                                                                                                                              
            CpuCores = 8                                                                                                                                                                   
        };                                                                                                                                                                                 
                                                                                                                                                                                         
        ProcessorEventArgs args = new(processInfos, stats);                                                                                                                                
                                                                                                                                                                                         
        Assert.Equal(stats, args.SystemStatistics);                                                                                                                                        
        Assert.Equal(8UL, args.SystemStatistics.CpuCores);                                                                                                                                 
    }                                      
}
