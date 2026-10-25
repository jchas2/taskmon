using System.Net;
using Task.Monitor.System.Services.Network;

namespace Task.Monitor.System.Services.Tests.Network;

public sealed class RoutableAddressTests
{
    [Theory]
    [InlineData("172.20.10.2",                 true)]
    [InlineData("100.101.102.103",             true)]
    [InlineData("fd7a:115c:a1e0::1",           true)]
    [InlineData("2001:db8::1",                 true)]
    [InlineData("169.254.12.34",               false)]
    [InlineData("127.0.0.1",                   false)]
    [InlineData("::1",                         false)]
    [InlineData("fe80::daa9:1a48:13bf:9080%18", false)]
    public void HasRoutableAddress_Ignores_Loopback_And_Link_Local(string address, bool expected)
    {
        Assert.Equal(expected, NetworkDeviceParser.HasRoutableAddress([IPAddress.Parse(address)]));
    }

    [Fact]
    public void HasRoutableAddress_Needs_Only_One_Routable_Address()
    {
        Assert.True(NetworkDeviceParser.HasRoutableAddress([IPAddress.Parse("fe80::1%4"), IPAddress.Parse("192.168.1.20")]));
        Assert.False(NetworkDeviceParser.HasRoutableAddress([]));
    }
}
