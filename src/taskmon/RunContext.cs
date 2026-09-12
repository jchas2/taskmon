using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System;
using Task.Monitor.System.Services;

namespace Task.Monitor;

public class RunContext(
    ServiceController serviceController,
    IFileSystem fileSystem,
    ISystemTerminal terminal,
    AppConfig appConfig)
{
    public ServiceController ServiceController => serviceController;
    public IFileSystem FileSystem { get; } = fileSystem;
    public ISystemTerminal Terminal { get; } = terminal;
    public AppConfig AppConfig { get; } = appConfig;
}
