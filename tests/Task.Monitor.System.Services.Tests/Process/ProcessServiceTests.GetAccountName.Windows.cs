#if __WIN32__
using System.Runtime.Versioning;
using System.Security.Principal;
using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed partial class ProcessServiceTests
{
    [Fact]
    [SupportedOSPlatform("windows")]
    public void GetAccountName_Returns_The_Name_Of_A_Well_Known_Account()
    {
        SecurityIdentifier localSystem = new(WellKnownSidType.LocalSystemSid, null);

        Assert.EndsWith("SYSTEM", ProcessService.GetAccountName(localSystem));
    }

    // An orphaned SID, such as a deleted account's, has no name. Translating it used to throw out of
    // the process sampling, losing the whole list and leaking its handles.
    [Fact]
    [SupportedOSPlatform("windows")]
    public void GetAccountName_Returns_The_Sid_For_An_Account_That_Does_Not_Exist()
    {
        SecurityIdentifier orphan = new("S-1-5-21-1-2-3-4242");

        Assert.Equal("S-1-5-21-1-2-3-4242", ProcessService.GetAccountName(orphan));
    }
}
#endif
