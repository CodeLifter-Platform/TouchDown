using Hangfire.Dashboard;

namespace TD.Services;

/// <summary>
/// Restricts the Hangfire dashboard to loopback callers.
///
/// TouchDown has no authentication of its own, and the dashboard can trigger, requeue and
/// delete jobs — so leaving it open to anything that can reach the port hands over job
/// control. Limiting it to the local machine matches the app's single-user, trusted-host
/// posture without inventing an auth system.
/// </summary>
public class LocalRequestsOnlyDashboardFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var connection = context.GetHttpContext().Connection;
        return IsLocal(connection.RemoteIpAddress, connection.LocalIpAddress);
    }

    /// <summary>The authorization decision, split out from the Hangfire context so it can be tested directly.</summary>
    internal static bool IsLocal(System.Net.IPAddress? remote, System.Net.IPAddress? local)
    {
        // No remote address at all means an in-process call.
        if (remote is null) return true;

        if (System.Net.IPAddress.IsLoopback(remote)) return true;

        // A request that reaches the server on the same address it came from is local
        // (covers container setups where loopback is presented as the host address).
        return local is not null && remote.Equals(local);
    }
}
