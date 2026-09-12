namespace Task.Monitor.System.Services.Startup;

// Where a startup entry is registered. Determines the enable/disable key that pairs with it and
// how its command is stored.
public enum StartupEntrySource
{
    RunKey,
    RunOnceKey,
    StartupFolder,
    ScheduledTask
}
