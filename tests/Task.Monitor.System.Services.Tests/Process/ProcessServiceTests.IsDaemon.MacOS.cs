#if __APPLE__
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed partial class ProcessServiceTests
{
    // launchd starts every app, agent and XPC service too, but as the logged-in user.
    [Theory]
    [InlineData(400, 1,   501u)]  // an app opened from the Dock
    [InlineData(401, 1,   502u)]  // another user's agent
    [InlineData(402, 350, 0u)]    // a root process started from a shell
    [InlineData(403, 350, 501u)]  // a command started from a shell
    public void IsDaemon_Is_False_For_User_Processes_And_Shell_Children(int pid, int parentPid, uint uid)
    {
        Assert.False(ProcessService.IsDaemon(pid, parentPid, uid));
    }

    [Theory]
    [InlineData(0,   0,   0u)]   // kernel_task
    [InlineData(1,   0,   0u)]   // launchd
    [InlineData(300, 1,   0u)]   // a root launch daemon
    [InlineData(301, 1,   88u)]  // a daemon run as a role account (_windowserver)
    [InlineData(302, 1,   499u)]
    public void IsDaemon_Is_True_For_The_Kernel_launchd_And_Its_System_Children(int pid, int parentPid, uint uid)
    {
        Assert.True(ProcessService.IsDaemon(pid, parentPid, uid));
    }
}
#endif
