namespace Task.Monitor.System.Services.Startup;

public enum StartupEntrySource
{
#if __WIN32__
    RunKey,
    RunOnceKey,
    StartupFolder,
    ScheduledTask,
#endif
#if __APPLE__
    OpenAtLogin,
    LoginHelper,
    LaunchAgent,
    LaunchDaemon,
#endif
}
