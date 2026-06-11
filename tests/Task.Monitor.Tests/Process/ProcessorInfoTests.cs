using System.Reflection;
using Task.Monitor.Process;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Process;

public sealed class ProcessorInfoTests
{
    [Fact]
    public void ProcessorInfo_Canary_Test() =>
        Assert.Equal(20, CanaryTestHelper.GetPropertyCount<ProcessorInfo>());
    
    [Fact]                                                                                                                             
    public void Default_Constructor_Initializes_With_Default_Values()                                                                  
    {                                                                                                                                  
        ProcessorInfo info = new();                                                                                                    
                                                                                                                                     
        Assert.Equal(0, info.Pid);                                                                                                     
        Assert.Equal(0, info.ThreadCount);                                                                                             
        Assert.Equal(0U, info.HandleCount);                                                                                            
        Assert.Equal(0, info.BasePriority);                                                                                            
        Assert.Equal(0, info.ParentPid);                                                                                               
        Assert.False(info.IsDaemon);                                                                                                   
        Assert.False(info.IsLowPriority);                                                                                              
        Assert.Equal(default(DateTime), info.StartTime);                                                                               
        Assert.Equal(string.Empty, info.ProcessName);                                                                                  
        Assert.Equal(string.Empty, info.FileDescription);                                                                              
        Assert.Equal(string.Empty, info.UserName);                                                                                     
        Assert.Equal(string.Empty, info.CmdLine);   
        Assert.Equal(0U, info.DiskReadBytes);
        Assert.Equal(0U, info.DiskWriteBytes);
        Assert.Equal(0, info.DiskUsage);                                                                                               
        Assert.Equal(0, info.UsedMemory);                                                                                              
        Assert.Equal(0.0, info.CpuTimePercent);                                                                                        
        Assert.Equal(0.0, info.CpuUserTimePercent);                                                                                    
        Assert.Equal(0.0, info.CpuKernelTimePercent);                                                                                  
        Assert.Equal(0.0, info.GpuTimePercent);
    }                                                                                                                                  
                                                                                                                                         
    [Fact]                                                                                                                             
    public void Properties_Can_Be_Set_And_Retrieved()                                                                                  
    {                                                                                                                                  
        DateTime startTime = DateTime.Now;                                                                                             
        ProcessorInfo info = new() {                                                                                                   
            Pid = 1234,                                                                                                                
            ThreadCount = 10,                                                                                                          
            HandleCount = 50,                                                                                                          
            BasePriority = 8,                                                                                                          
            ParentPid = 1,                                                                                                             
            IsDaemon = true,                                                                                                           
            IsLowPriority = false,                                                                                                     
            StartTime = startTime,                                                                                                     
            ProcessName = "test.exe",                                                                                                  
            FileDescription = "Test Process",                                                                                          
            UserName = "testuser",                                                                                                     
            CmdLine = "/usr/bin/test --arg",    
            DiskReadBytes = 999888,
            DiskWriteBytes = 222333,
            DiskUsage = 1024,                                                                                                          
            UsedMemory = 2048,                                                                                                         
            CpuTimePercent = 15.5,                                                                                                     
            CpuUserTimePercent = 10.2,                                                                                                 
            CpuKernelTimePercent = 5.3,                                                                                                
            GpuTimePercent = 4.1,
        };                                                                                                                             
                                                                                                                                     
        Assert.Equal(1234, info.Pid);                                                                                                  
        Assert.Equal(10, info.ThreadCount);                                                                                            
        Assert.Equal(50U, info.HandleCount);                                                                                           
        Assert.Equal(8, info.BasePriority);                                                                                            
        Assert.Equal(1, info.ParentPid);                                                                                               
        Assert.True(info.IsDaemon);                                                                                                    
        Assert.False(info.IsLowPriority);                                                                                              
        Assert.Equal(startTime, info.StartTime);                                                                                       
        Assert.Equal("test.exe", info.ProcessName);                                                                                    
        Assert.Equal("Test Process", info.FileDescription);                                                                            
        Assert.Equal("testuser", info.UserName);                                                                                       
        Assert.Equal("/usr/bin/test --arg", info.CmdLine);  
        Assert.Equal(999888U, info.DiskReadBytes);
        Assert.Equal(222333U, info.DiskWriteBytes);
        Assert.Equal(1024, info.DiskUsage);                                                                                            
        Assert.Equal(2048, info.UsedMemory);                                                                                           
        Assert.Equal(15.5, info.CpuTimePercent);                                                                                       
        Assert.Equal(10.2, info.CpuUserTimePercent);                                                                                   
        Assert.Equal(5.3, info.CpuKernelTimePercent);                                                                                  
        Assert.Equal(4.1, info.GpuTimePercent);
    }                                                                                                                                  
}
