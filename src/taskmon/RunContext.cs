using Task.Monitor.Cli.Utils;
using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System;
using Task.Monitor.System.Process;
using IProcessor = Task.Monitor.Process.IProcessor;

namespace Task.Monitor;

public class RunContext(
    IFileSystem fileSystem,
    ISystemTerminal terminal,
    IProcessService processService,
    IModuleService moduleService,
    IThreadService threadService,
    IProcessor processor,
    AppConfig appConfig)
{
    public IFileSystem FileSystem { get; } = fileSystem;
    public ISystemTerminal Terminal { get; } = terminal;
    public IProcessService ProcessService { get; } = processService;
    public IModuleService ModuleService { get; } = moduleService;
    public IThreadService ThreadService { get; } = threadService;
    public IProcessor Processor { get; } = processor;
    public AppConfig AppConfig { get; } = appConfig;
}
