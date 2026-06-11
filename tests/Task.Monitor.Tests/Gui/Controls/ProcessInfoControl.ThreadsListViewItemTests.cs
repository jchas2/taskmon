using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Process;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ThreadsListViewItemTests
{
    private readonly AppConfig appConfig;                                                                                                                                                  
    private readonly Mock<IFileSystem> fileSystem = new();                                                                                                                                 
                                                                                                                                                                                             
    public ThreadsListViewItemTests() =>                                                                                                                                                    
        appConfig = new AppConfig(fileSystem.Object);                                                                                                                                      
                                                                                                                                                                                             
    [Fact]                                                                                                                                                                                 
    public void Constructor_With_Valid_Args_Initialises_SubItems_Successfully()                                                                                                            
    {                                                                                                                                                                                      
        ThreadInfo threadInfo = new() {                                                                                                                                                    
            ThreadId = 1868067040,                                                                                                                                                         
            ThreadState = "Running",                                                                                                                                                       
            Reason = "",                                                                                                                                                                   
            Priority = 8,                                                                                                                                                                  
            StartAddress = 0x0000000000000000,                                                                                                                                             
            CpuKernelTime = TimeSpan.FromSeconds(127),                                                                                                                                     
            CpuUserTime = TimeSpan.FromSeconds(581),                                                                                                                                       
            CpuTotalTime = TimeSpan.FromSeconds(708)                                                                                                                                       
        };                                                                                                                                                                                 
                                                                                                                                                                                         
        ProcessInfoControl.ThreadListViewItem item = new(threadInfo, appConfig);                                                                                                           
                                                                                                                                                                                         
        Assert.NotNull(item);                                                                                                                                                              
        Assert.Equal(1868067040, item.ThreadId);                                                                                                                                           
        Assert.Equal("1868067040", item.Text);                                                                                                                                             
        Assert.Equal("1868067040", item.SubItems[(int)ProcessInfoControl.ThreadColumns.Id].Text);                                                                                          
        Assert.Equal("Running", item.SubItems[(int)ProcessInfoControl.ThreadColumns.State].Text);                                                                                          
        Assert.Equal("", item.SubItems[(int)ProcessInfoControl.ThreadColumns.Reason].Text);                                                                                                
        Assert.Equal("8", item.SubItems[(int)ProcessInfoControl.ThreadColumns.Priority].Text);                                                                                             
        Assert.Equal("0x0000000000000000", item.SubItems[(int)ProcessInfoControl.ThreadColumns.StartAddress].Text);                                                                        
        Assert.Equal(TimeSpan.FromSeconds(127).ToString(), item.SubItems[(int)ProcessInfoControl.ThreadColumns.CpuKernelTime].Text);                                                       
        Assert.Equal(TimeSpan.FromSeconds(581).ToString(), item.SubItems[(int)ProcessInfoControl.ThreadColumns.CpuUserTime].Text);                                                         
        Assert.Equal(TimeSpan.FromSeconds(708).ToString(), item.SubItems[(int)ProcessInfoControl.ThreadColumns.CpuTotalTime].Text);                                                        
    }                                                                                                                                                                                      
    
    [Fact]                                                                                                                                                                                 
    public void Constructor_Sets_Background_Color_For_All_SubItems()                                                                                                                       
    {                                                                                                                                                                                      
        ThreadInfo threadInfo = new() {                                                                                                                                                    
            ThreadId = 12345,                                                                                                                                                              
            ThreadState = "Running",                                                                                                                                                       
            Reason = "Test",                                                                                                                                                               
            Priority = 8,                                                                                                                                                                  
            StartAddress = 0x1000000,                                                                                                                                                      
            CpuKernelTime = new TimeSpan(hours: 0, minutes: 2, seconds: 32),                                                                                                                                                 
            CpuUserTime = new TimeSpan(hours: 0, minutes: 3, seconds: 12),                                                                                                               
            CpuTotalTime = new TimeSpan(hours: 0, minutes: 5, seconds: 44),                                                                                                                                                   
        };                                                                                                                                                                                 
                                                                                                                                                                                         
        ProcessInfoControl.ThreadListViewItem item = new(threadInfo, appConfig);                                                                                                           
                                                                                                                                                                                         
        for (int i = 0; i < (int)ProcessInfoControl.ThreadColumns.Count; i++) {                                                                                                            
            Assert.Equal(                                                                                                                                                                  
                appConfig.DefaultTheme.Background,                                                                                                                                         
                item.SubItems[i].BackgroundColor);                                                                                                                                         
        }                                                                                                                                                                                  
    }                                                                                                                                                                                      
                                                                                                                                                                                             
    public static TheoryData<int, ConsoleColor> ThreadColourData()                                                                                                                              
        => new() {                                                                                                                                                                         
            { (int)ProcessInfoControl.ThreadColumns.State, ConsoleColor.DarkYellow },                                                                                                                     
            { (int)ProcessInfoControl.ThreadColumns.Reason, ConsoleColor.DarkYellow },                                                                                                                     
            { (int)ProcessInfoControl.ThreadColumns.Priority, ConsoleColor.DarkYellow },                                                                                                                     
            { (int)ProcessInfoControl.ThreadColumns.CpuKernelTime, ConsoleColor.DarkYellow },                                                                                                                     
            { (int)ProcessInfoControl.ThreadColumns.CpuUserTime, ConsoleColor.DarkYellow },                                                                                                                     
            { (int)ProcessInfoControl.ThreadColumns.CpuTotalTime, ConsoleColor.DarkYellow },                                                                                                                     
            { (int)ProcessInfoControl.ThreadColumns.Id, ConsoleColor.White }                                                                                                                     
        };                                                                                                                                                                                 
                                                                                                                                                                                         
    [Theory]                                                                                                                                                                               
    [MemberData(nameof(ThreadColourData))]                                                                                                                                                   
    public void Constructor_Sets_Foreground_Color_Changed_For_All_SubItems(
        int column, 
        ConsoleColor foregroundColour)                                                                                                                       
    {                                                                                                                                                                                      
        ThreadInfo threadInfo = new() {                                                                                                                                                    
            ThreadId = 12345,                                                                                                                                                              
            ThreadState = "Running",                                                                                                                                                       
            Reason = "Test",                                                                                                                                                               
            Priority = 8,                                                                                                                                                                  
            StartAddress = 0x1000000,                                                                                                                                                      
            CpuKernelTime = new TimeSpan(hours: 0, minutes: 2, seconds: 32),                                                                                                                                                 
            CpuUserTime = new TimeSpan(hours: 0, minutes: 3, seconds: 12),                                                                                                               
            CpuTotalTime = new TimeSpan(hours: 0, minutes: 5, seconds: 44),                                                                                                                                                   
        };                                                                                                                                                                                 
                                                                                                                                                                                         
        ProcessInfoControl.ThreadListViewItem item = new(threadInfo, appConfig);                                                                                                           
                                       
        Assert.Equal(foregroundColour, item.SubItems[column].ForegroundColor);                                                                                                                                         
    }                                                                                                                                                                                      
                                                                                                                                                                                             
    [Fact]                                                                                                                                                                                 
    public void Constructor_Creates_Correct_Number_Of_SubItems()                                                                                                                           
    {                                                                                                                                                                                      
        ThreadInfo threadInfo = new() {                                                                                                                                                    
            ThreadId = 12345,                                                                                                                                                              
            ThreadState = "Running",                                                                                                                                                       
            Reason = "Test",                                                                                                                                                               
            Priority = 8,                                                                                                                                                                  
            StartAddress = 0x1000000,                                                                                                                                                      
            CpuKernelTime = TimeSpan.Zero,                                                                                                                                                 
            CpuUserTime = TimeSpan.Zero,                                                                                                                                                   
            CpuTotalTime = TimeSpan.Zero                                                                                                                                                   
        };                                                                                                                                                                                 
                                                                                                                                                                                         
        ProcessInfoControl.ThreadListViewItem item = new(threadInfo, appConfig);                                                                                                           
                                                                                                                                                                                         
        Assert.Equal((int)ProcessInfoControl.ThreadColumns.Count, item.SubItems.Count());                                                                                                    
    }                                                                                                                                                                                      
                                                                                                                                                                                             
    [Fact]                                                                                                                                                                                 
    public void UpdateItem_Updates_Text_Values()                                                                                                                                           
    {                                                                                                                                                                                      
        ThreadInfo initialInfo = new() {                                                                                                                                                   
            ThreadId = 1234,                                                                                                                                                               
            ThreadState = "Running",                                                                                                                                                       
            Reason = "UserRequest",                                                                                                                                                        
            Priority = 8,                                                                                                                                                                  
            StartAddress = 0x1000000,                                                                                                                                                      
            CpuKernelTime = TimeSpan.FromSeconds(10),                                                                                                                                      
            CpuUserTime = TimeSpan.FromSeconds(20),                                                                                                                                        
            CpuTotalTime = TimeSpan.FromSeconds(30)                                                                                                                                        
        };                                                                                                                                                                                 
                                                                                                                                                                                         
        ProcessInfoControl.ThreadListViewItem item = new(initialInfo, appConfig);                                                                                                          
                                                                                                                                                                                         
        ThreadInfo updatedInfo = new() {                                                                                                                                                   
            ThreadId = 1234,                                                                                                                                                               
            ThreadState = "Waiting",                                                                                                                                                       
            Reason = "Lock",                                                                                                                                                               
            Priority = 16,                                                                                                                                                                 
            StartAddress = 0x1000000,                                                                                                                                                      
            CpuKernelTime = TimeSpan.FromSeconds(15),                                                                                                                                      
            CpuUserTime = TimeSpan.FromSeconds(25),                                                                                                                                        
            CpuTotalTime = TimeSpan.FromSeconds(40)                                                                                                                                        
        };                                                                                                                                                                                 
                                                                                                                                                                                         
        item.UpdateItem(updatedInfo);                                                                                                                                                      
                                                                                                                                                                                         
        Assert.Equal("Waiting", item.SubItems[(int)ProcessInfoControl.ThreadColumns.State].Text);                                                                                          
        Assert.Equal("Lock", item.SubItems[(int)ProcessInfoControl.ThreadColumns.Reason].Text);                                                                                            
        Assert.Equal("16", item.SubItems[(int)ProcessInfoControl.ThreadColumns.Priority].Text);                                                                                            
        Assert.Equal(TimeSpan.FromSeconds(15).ToString(), item.SubItems[(int)ProcessInfoControl.ThreadColumns.CpuKernelTime].Text);                                                        
        Assert.Equal(TimeSpan.FromSeconds(25).ToString(), item.SubItems[(int)ProcessInfoControl.ThreadColumns.CpuUserTime].Text);                                                          
        Assert.Equal(TimeSpan.FromSeconds(40).ToString(), item.SubItems[(int)ProcessInfoControl.ThreadColumns.CpuTotalTime].Text);                                                         
    }                                                                                                                                                                                      
                                                                                                                                                                                             
    [Theory]                                                                                                                                                                               
    [InlineData(0x0)]                                                                                                                                                                      
    [InlineData(0x1000)]                                                                                                                                                                   
    [InlineData(0x7FFF12345678)]                                                                                                                                                           
    public void Constructor_Formats_StartAddress_As_Hexadecimal(long startAddress)                                                                                                         
    {                                                                                                                                                                                      
        ThreadInfo threadInfo = new() {                                                                                                                                                    
            ThreadId = 1234,                                                                                                                                                               
            ThreadState = "Running",                                                                                                                                                       
            Reason = "Test",                                                                                                                                                               
            Priority = 8,                                                                                                                                                                  
            StartAddress = startAddress,                                                                                                                                                   
            CpuKernelTime = TimeSpan.Zero,                                                                                                                                                 
            CpuUserTime = TimeSpan.Zero,                                                                                                                                                   
            CpuTotalTime = TimeSpan.Zero                                                                                                                                                   
        };                                                                                                                                                                                 
                                                                                                                                                                                         
        ProcessInfoControl.ThreadListViewItem item = new(threadInfo, appConfig);                                                                                                           
                                                                                                                                                                                         
        string addressText = item.SubItems[(int)ProcessInfoControl.ThreadColumns.StartAddress].Text;                                                                                       
        Assert.StartsWith("0x", addressText);                                                                                                                                              
        Assert.Equal(18, addressText.Length); // "0x" + 16 hex digits                                                                                                                      
    }                                                                                                                                                                                      
}