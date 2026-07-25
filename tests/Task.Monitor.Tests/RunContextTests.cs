using Moq;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.Process;
using Task.Monitor.System;
using Task.Monitor.System.Process;

namespace Task.Monitor.Tests;

public class RunContextTests
{
    [Fact]
    public void Should_Create_RunContext()
    {
        Mock<IFileSystem> fileSystem = new();
        Mock<ISystemTerminal> terminal = new();
        Mock<IProcessService> processService = new();
        Mock<IModuleService> moduleService = new();
        Mock<IThreadService> threadService = new();
        Mock<IProcessor> processor = new();
        Mock<IOutputWriter> outputWriter = new();
        AppConfig appConfig = new(fileSystem.Object);

        RunContext context = new(
            fileSystem.Object,
            terminal.Object,
            processService.Object,
            moduleService.Object,
            threadService.Object,
            processor.Object,
            appConfig);
        
        Assert.True(context.FileSystem == fileSystem.Object);
        Assert.True(context.Terminal == terminal.Object);
        Assert.True(context.ProcessService == processService.Object);
        Assert.True(context.ModuleService == moduleService.Object);
        Assert.True(context.ThreadService == threadService.Object);
        Assert.True(context.Processor == processor.Object);
        Assert.True(context.AppConfig == appConfig);
    }
}
