#if __APPLE__
namespace Task.Monitor.System.Services.Startup;

public partial class StartupService
{
    private partial StartupToggleResult ApplyEnabled(StartupEntry entry, bool enabled) =>
        StartupToggleResult.NotSupported;
}
#endif
