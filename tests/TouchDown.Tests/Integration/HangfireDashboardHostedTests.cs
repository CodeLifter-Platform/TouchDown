using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.Integration;

/// <summary>
/// TouchDown has no authentication: it is a single-user app on a trusted host, and every
/// page and the hub are open by design. The one closed surface is the Hangfire dashboard,
/// which can trigger and delete jobs: off by default outside Development, and loopback-only
/// wherever it is on. These host the real app and check both halves of that.
/// </summary>
[Collection(HostedAppCollection.Name)]
public class HangfireDashboardHostedTests
{
    private static readonly Dictionary<string, string?> DashboardOn = new() { ["Hangfire:EnableDashboard"] = "true" };

    [Fact]
    public async Task The_dashboard_is_absent_in_production_by_default()
    {
        using var app = new HostedApp("Production");
        using var client = app.CreateClient();

        var response = await client.GetAsync("/hangfire");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task When_enabled_the_dashboard_serves_a_local_caller()
    {
        using var app = new HostedApp("Production", settings: DashboardOn);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/hangfire");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task When_enabled_the_dashboard_refuses_a_caller_from_another_machine()
    {
        using var app = new HostedApp("Production",
            testServices: services => services.AddSingleton<IStartupFilter>(
                new RemoteCallerFilter(IPAddress.Parse("192.168.1.20"), IPAddress.Parse("192.168.1.2"))),
            settings: DashboardOn);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/hangfire");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }

    [Fact]
    public async Task The_health_route_is_open_to_everyone()
    {
        using var app = new HostedApp("Production",
            testServices: services => services.AddSingleton<IStartupFilter>(
                new RemoteCallerFilter(IPAddress.Parse("192.168.1.20"), IPAddress.Parse("192.168.1.2"))));
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable });
    }

    /// <summary>
    /// Stamps every request with a remote address, the way a reverse proxy or a LAN caller
    /// would arrive. The TestServer otherwise presents no address at all.
    /// </summary>
    private sealed class RemoteCallerFilter(IPAddress remote, IPAddress local) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, pipeline) =>
            {
                context.Connection.RemoteIpAddress = remote;
                context.Connection.LocalIpAddress = local;
                await pipeline(context);
            });
            next(app);
        };
    }
}
