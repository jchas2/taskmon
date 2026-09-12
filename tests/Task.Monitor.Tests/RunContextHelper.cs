using Moq;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System;
using Task.Monitor.System.Process;
using Task.Monitor.System.Tests.Controls;

using System.Drawing;
using Task.Monitor.System.Services;

namespace Task.Monitor.Tests;

internal class RunContextHelper
{
    internal ServiceController serviceController = new();
    // Internal for Mock Verification pattern.
    internal Mock<IFileSystem> fileSystem = new();
    internal Mock<ISystemTerminal> terminal = new();
    //internal Mock<IOutputWriter> outputWriter = new();
    internal AppConfig appConfig;

    public RunContextHelper()
    {
        appConfig = new(fileSystem.Object);
        
        terminal.Setup(t => t.WindowHeight).Returns(32);
        terminal.Setup(t => t.WindowWidth).Returns(32);
        terminal.Setup(t => t.BackgroundColor).Returns(ConsolePalette.Black);
        terminal.Setup(t => t.ForegroundColor).Returns(ConsolePalette.White);
        terminal.Setup(t => t.KeyAvailable).Returns(false);
    }

    internal RunContext GetRunContext() =>
        new RunContext(
            serviceController,
            fileSystem.Object,
            new ForwardingTerminal(terminal.Object),
            appConfig);
    //outputWriter.Object);
}
