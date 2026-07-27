using System.Net;
using TD.Services;

namespace TouchDown.Tests.Services;

/// <summary>
/// The Hangfire dashboard can trigger, requeue and delete jobs, and TouchDown has no
/// authentication of its own — so anything that can reach the port could otherwise drive
/// the job system. Access is limited to callers on the local machine.
/// </summary>
public class HangfireDashboardFilterTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.53")]
    [InlineData("::1")]
    public void LoopbackCallersAreAllowed(string remote)
    {
        Assert.True(LocalRequestsOnlyDashboardFilter.IsLocal(IPAddress.Parse(remote), IPAddress.Parse("127.0.0.1")));
    }

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("192.168.1.20")]
    [InlineData("172.17.0.4")]
    [InlineData("203.0.113.9")]
    public void RemoteCallersAreDenied(string remote)
    {
        Assert.False(LocalRequestsOnlyDashboardFilter.IsLocal(IPAddress.Parse(remote), IPAddress.Parse("10.0.0.1")));
    }

    [Fact]
    public void InProcessCallWithNoRemoteAddressIsAllowed()
    {
        Assert.True(LocalRequestsOnlyDashboardFilter.IsLocal(null, IPAddress.Parse("10.0.0.1")));
    }

    [Fact]
    public void SameHostAddressIsAllowed()
    {
        // Container networking can present a local request on the host address rather than loopback.
        var address = IPAddress.Parse("172.17.0.2");

        Assert.True(LocalRequestsOnlyDashboardFilter.IsLocal(address, address));
    }

    [Fact]
    public void RemoteCallerIsDeniedWhenLocalAddressIsUnknown()
    {
        // Fail closed rather than open when the local address can't be determined.
        Assert.False(LocalRequestsOnlyDashboardFilter.IsLocal(IPAddress.Parse("192.168.1.20"), null));
    }
}
