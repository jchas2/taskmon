using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.Tests.Gui.Controls;

// Sample sources for the current platform, and the text StartupControl draws for each.
internal static class StartupTestSources
{
#if __WIN32__
    public const StartupEntrySource Primary       = StartupEntrySource.RunKey;
    public const string             PrimaryText   = "Run";
    public const StartupEntrySource Secondary     = StartupEntrySource.StartupFolder;
    public const string             SecondaryText = "Startup Folder";
#endif
#if __APPLE__
    public const StartupEntrySource Primary       = StartupEntrySource.LaunchAgent;
    public const string             PrimaryText   = "Launch Agent";
    public const StartupEntrySource Secondary     = StartupEntrySource.LoginHelper;
    public const string             SecondaryText = "Login Helper";
#endif
}
