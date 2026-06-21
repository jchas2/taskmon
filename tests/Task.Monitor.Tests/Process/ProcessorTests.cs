using Moq;
using Task.Monitor.Process;
using Task.Monitor.System.Process;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.Tests.Process;

public sealed class ProcessorTests
{
      [Fact]                                                                                                                             
      public void Run_With_Single_Process_Completes_One_Iteration()                                                                      
      {                                                                                                                                  
          ProcessServiceFake processService = new();
                                                                                                                                         
          using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
          ProcessInfo? currentProcessInfo = new ProcessService().GetProcessById(currentProcess.Id);

          Assert.NotNull(currentProcessInfo);
          
          processService.AddProcessInfo(currentProcessInfo);                                                                             
                                                                                                                                         
          Processor processor = new(processService) {                                                                                    
              IterationLimit = 1,                                                                               
              Delay = Processor.MinimumDelayInMilliseconds                                         
          };                                                                                                                             
                                                                                                                                         
          bool eventRaised = false;                                                                                                      
          ProcessorEventArgs? capturedArgs = null;                                                                                       
                                                                                                                                         
          processor.ProcessorUpdated += (sender, args) => {                                                                              
              eventRaised = true;                                                                                                        
              capturedArgs = args;                                                                                                       
          };                                                                                                                             
                                                                                                                                         
          processor.Run();                                                                                                               
                                                                                                                                         
          // Wait for the iteration to complete.                                                                                          
          // One iteration includes: process collection + delay + CPU calculation + delay for monitor.                                    
          // So we wait for at least 2 * delay plus some buffer.                                                                          
          int waitTime = (Processor.MinimumDelayInMilliseconds * 2) + 1000;                                                              
          Thread.Sleep(waitTime);                                                                                                        
                                                                                                                                         
          processor.Stop();                                                                                                              

          Assert.Equal(1, processor.ProcessCount);                                                                                       
          Assert.True(processor.ThreadCount > 0, "Processor should track thread count");                                                        

          Assert.True(eventRaised, "ProcessorUpdated event should have been raised");                                                    
          Assert.NotNull(capturedArgs);                                                                                                  
          Assert.NotNull(capturedArgs.ProcessInfos);                                                                                     
          Assert.NotEmpty(capturedArgs.ProcessInfos);                                                                                    
          Assert.Single(capturedArgs.ProcessInfos);                                                                                      
                                                                                                                                         
          ProcessorInfo processInfo = capturedArgs.ProcessInfos[0];                                                                      
          
          Assert.Equal(currentProcess.Id, processInfo.Pid);                                                                              
          Assert.Equal(currentProcess.ProcessName, processInfo.ProcessName);                                                             
          Assert.True(processInfo.CpuTimePercent >= 0.0);                                                                                
          Assert.True(processInfo.CpuUserTimePercent >= 0.0);                                                                            
          Assert.True(processInfo.CpuKernelTimePercent >= 0.0);                                                                          
          Assert.True(processInfo.ThreadCount > 0, "Process should have at least one thread");                                                  
          Assert.Equal(processInfo.ThreadCount, processor.ThreadCount);          

          Assert.Equal(1, capturedArgs.Statistics.ProcessCount);                                                                   

          Assert.True(capturedArgs.Statistics.CpuPercentIdleTime >= 0.0);                                                          
          Assert.True(capturedArgs.Statistics.CpuPercentUserTime >= 0.0);                                                          
          Assert.True(capturedArgs.Statistics.CpuPercentKernelTime >= 0.0); 
          Assert.True(capturedArgs.Statistics.TotalPhysical > 0);                                                                  
          Assert.True(capturedArgs.Statistics.AvailablePhysical > 0);
      }
      
      [Fact]                                                                                                                                    
      public void Stop_Cancels_Running_Processor()                                                                                              
      {                                                                                                                                         
          ProcessServiceFake processService = new();
                                                                                                                                                
          using SysDiag::Process currentProcess = SysDiag::Process.GetCurrentProcess();
          ProcessInfo? currentProcessInfo = new ProcessService().GetProcessById(currentProcess.Id);

          Assert.NotNull(currentProcessInfo);
          
          processService.AddProcessInfo(currentProcessInfo);                                                                                    
                                                                                                                                                
          Processor processor = new(processService) {                                                                                           
              IterationLimit = 0,  // Run indefinitely.                                                                                          
              Delay = Processor.MinimumDelayInMilliseconds * 2                                                                                      
          };                                                                                                                                    
          
          processor.Run();                                                                                                                      
          Thread.Sleep(Processor.MinimumDelayInMilliseconds * 2);                                                                                            
          processor.Stop();                                                                                           
          
          Assert.False(processor.IsRunning);
      }                            
}
