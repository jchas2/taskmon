using Moq;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System;
using Task.Monitor.System.Services;

namespace Task.Monitor.Tests;

public class RunContextTests
{
    [Fact]
    public void Should_Create_RunContext()
    {
        ServiceController serviceController = new();
        Mock<IFileSystem> fileSystem = new();
        Mock<ISystemTerminal> terminal = new();
        Mock<IOutputWriter> outputWriter = new();
        AppConfig appConfig = new(fileSystem.Object);

        RunContext context = new(
            serviceController,
            fileSystem.Object,
            terminal.Object,
            appConfig);
        
        Assert.True(context.ServiceController == serviceController);
        Assert.True(context.FileSystem == fileSystem.Object);
        Assert.True(context.Terminal == terminal.Object);
        Assert.True(context.AppConfig == appConfig);
    }
}
