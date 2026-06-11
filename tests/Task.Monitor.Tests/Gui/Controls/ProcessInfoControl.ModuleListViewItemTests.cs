using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Gui.Controls;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Process;

namespace Task.Monitor.Tests.Gui.Controls;

public sealed class ModuleListViewItemTests
{
    private readonly AppConfig appConfig;                                                                                                                                                  
    private readonly Mock<IFileSystem> fileSystem = new();                                                                                                                                 
                                                                                                                                                                                         
    public ModuleListViewItemTests() =>                                                                                                                                                    
        appConfig = new AppConfig(fileSystem.Object);                                                                                                                                      
    
    public static TheoryData<string, string> ModuleInfoData()                                                                                                                              
        => new() {                                                                                                                                                                         
            { "kernel32.dll", @"C:\Windows\System32\kernel32.dll" },                                                                                                                     
            { "libSystem.B.dylib", "/usr/lib/libSystem.B.dylib" },                                                                                                                         
            { "libc.so.6", "/lib/x86_64-linux-gnu/libc.so.6" },                                                                                                                            
            { "User32.dll", @"C:\Windows\System32\User32.dll" }                                                                                                                          
        };                                                                                                                                                                                 
                                                                                                                                                                                         
    [Theory]                                                                                                                                                                               
    [MemberData(nameof(ModuleInfoData))]                                                                                                                                                   
    public void Constructor_With_Valid_Args_Initialises_SubItems_Successfully(                                                                                                             
        string moduleName,                                                                                                                                                                 
        string fileName)                                                                                                                                                                   
    {                                                                                                                                                                                      
        ModuleInfo moduleInfo = new() {                                                                                                                                                    
            ModuleName = moduleName,                                                                                                                                                       
            FileName = fileName                                                                                                                                                            
        };                                                                                                                                                                                 
                                                                                                                                                                                         
        ProcessInfoControl.ModuleListViewItem item = new(moduleInfo, appConfig);                                                                                                           
                                                                                                                                                                                         
        Assert.Equal(moduleName, item.Text);                                                                                                                                               
        Assert.Equal(moduleName, item.SubItems[(int)ProcessInfoControl.ModuleColumns.ModuleName].Text);                                                                                    
        Assert.Equal(fileName, item.SubItems[(int)ProcessInfoControl.ModuleColumns.FileName].Text);                                                                                        
    }                                                                                                                                                                                      
                                                                                                                                                                                         
    [Fact]                                                                                                                                                                                 
    public void Constructor_Sets_Background_Color_For_All_SubItems()                                                                                                                       
    {                                                                                                                                                                                      
        ModuleInfo moduleInfo = new() {                                                                                                                                                    
            ModuleName = "TestModule",                                                                                                                                                     
            FileName = "/path/to/TestModule.dll"                                                                                                                                           
        };                                                                                                                                                                                 
                                                                                                                                                                                         
        ProcessInfoControl.ModuleListViewItem item = new(moduleInfo, appConfig);                                                                                                           
                                                                                                                                                                                         
        for (int i = 0; i < (int)ProcessInfoControl.ModuleColumns.Count; i++) {                                                                                                            
            Assert.Equal(                                                                                                                                                                  
                appConfig.DefaultTheme.Background,                                                                                                                                         
                item.SubItems[i].BackgroundColor);                                                                                                                                         
        }                                                                                                                                                                                  
    }                                                                                                                                                                                      
                                                                                                                                                                                         
    [Fact]                                                                                                                                                                                 
    public void Constructor_Sets_Foreground_Color_For_All_SubItems()                                                                                                                       
    {                                                                                                                                                                                      
        ModuleInfo moduleInfo = new() {                                                                                                                                                    
            ModuleName = "TestModule",                                                                                                                                                     
            FileName = "/path/to/TestModule.dll"                                                                                                                                           
        };                                                                                                                                                                                 
                                                                                                                                                                                         
        ProcessInfoControl.ModuleListViewItem item = new(moduleInfo, appConfig);                                                                                                           
                                                                                                                                                                                         
        for (int i = 0; i < (int)ProcessInfoControl.ModuleColumns.Count; i++) {                                                                                                            
            Assert.Equal(                                                                                                                                                                  
                appConfig.DefaultTheme.Foreground,                                                                                                                                         
                item.SubItems[i].ForegroundColor);                                                                                                                                         
        }                                                                                                                                                                                  
    }                                                                                                                                                                                      
                                                                                                                                                                                         
    [Fact]                                                                                                                                                                                 
    public void Constructor_Creates_Correct_Number_Of_SubItems()                                                                                                                           
    {                                                                                                                                                                                      
        ModuleInfo moduleInfo = new() {                                                                                                                                                    
            ModuleName = "TestModule",                                                                                                                                                     
            FileName = "/path/to/TestModule.dll"                                                                                                                                           
        };                                                                                                                                                                                 
                                                                                                                                                                                         
        ProcessInfoControl.ModuleListViewItem item = new(moduleInfo, appConfig);                                                                                                           
                                                                                                                                                                                         
        Assert.Equal((int)ProcessInfoControl.ModuleColumns.Count, item.SubItems.Count());                                                                                                    
    }                                                                                                                                                                                      
    
    public static TheoryData<string, string> LongPathData()                                                                                                                                
        => new() {                                                                                                                                                                         
            { "VeryLongModuleName.With.Multiple.Namespaces.dll", "/usr/local/lib/very/long/path/to/module/VeryLongModuleName.With.Multiple.Namespaces.dll" },                              
            { "Short.dll", "/short" },                                                                                                                                                     
            { "M", "/m" }                                                                                                                                                                  
        };                                                                                                                                                                                 
                                                                                                                                                                                         
    [Theory]                                                                                                                                                                               
    [MemberData(nameof(LongPathData))]                                                                                                                                                     
    public void Constructor_With_Various_Length_Paths_Creates_Item_Successfully(                                                                                                           
        string moduleName,                                                                                                                                                                 
        string fileName)                                                                                                                                                                   
    {                                                                                                                                                                                      
        ModuleInfo moduleInfo = new() {                                                                                                                                                    
            ModuleName = moduleName,                                                                                                                                                       
            FileName = fileName                                                                                                                                                            
        };                                                                                                                                                                                 
                                                                                                                                                                                         
        ProcessInfoControl.ModuleListViewItem item = new(moduleInfo, appConfig);                                                                                                           
                                                                                                                                                                                         
        Assert.NotNull(item);                                                                                                                                                              
        Assert.Equal(moduleName, item.Text);                                                                                                                                               
        Assert.Equal(fileName, item.SubItems[(int)ProcessInfoControl.ModuleColumns.FileName].Text);                                                                                        
    }                                   
}