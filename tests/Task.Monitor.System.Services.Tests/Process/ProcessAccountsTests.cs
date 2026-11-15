using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed class ProcessAccountsTests
{
    // Windows: SYSTEM is root; the service accounts, per-service accounts (NT SERVICE\...), Window
    // Manager (DWM-n) and font driver host (UMFD-n) accounts are system; anyone else is a user.
    [Theory]
    [InlineData("S-1-5-18",                                                  ProcessAccount.Root)]
    [InlineData("S-1-5-19",                                                  ProcessAccount.System)]
    [InlineData("S-1-5-20",                                                  ProcessAccount.System)]
    [InlineData("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464", ProcessAccount.System)]
    [InlineData("S-1-5-90-0-1",                                              ProcessAccount.System)]
    [InlineData("S-1-5-96-0-1",                                              ProcessAccount.System)]
    [InlineData("S-1-5-21-1004336348-1177238915-682003330-1001",             ProcessAccount.User)]
    [InlineData("S-1-5-180",                                                 ProcessAccount.User)]
    public void FromSid_Names_The_Account_Kind(string sid, ProcessAccount expected) =>
        Assert.Equal(expected, ProcessAccounts.FromSid(sid));

    // macOS: uid 0 is root, the _-prefixed daemon accounts sit below 500, and nobody is -2.
    [Theory]
    [InlineData(0u,          ProcessAccount.Root)]
    [InlineData(1u,          ProcessAccount.System)]
    [InlineData(88u,         ProcessAccount.System)]
    [InlineData(499u,        ProcessAccount.System)]
    [InlineData(4294967294u, ProcessAccount.System)]
    [InlineData(500u,        ProcessAccount.User)]
    [InlineData(501u,        ProcessAccount.User)]
    public void FromUid_Names_The_Account_Kind(uint uid, ProcessAccount expected) =>
        Assert.Equal(expected, ProcessAccounts.FromUid(uid));
}
