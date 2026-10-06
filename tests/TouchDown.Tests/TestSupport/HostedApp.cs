using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace TouchDown.Tests.TestSupport;

/// <summary>
/// Boots the real application the way <c>dotnet run</c> does, in-process, against throwaway
/// SQLite files for both the app database and Hangfire's job storage. Every hosted test goes
/// through here so none of them leaves a database behind in the working directory.
/// </summary>
public sealed class HostedApp : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly Action<IServiceCollection>? _testServices;
    private readonly Dictionary<string, string?> _settings;
    private readonly string _dbPath;
    private readonly string _hangfirePath;

    /// <param name="environment">The ASP.NET environment to boot in; Production is the shipped default.</param>
    /// <param name="testServices">Service overrides applied after the app's own registrations.</param>
    /// <param name="settings">Extra configuration keys layered over appsettings.</param>
    public HostedApp(
        string environment = "Production",
        Action<IServiceCollection>? testServices = null,
        Dictionary<string, string?>? settings = null)
    {
        _environment = environment;
        _testServices = testServices;
        _settings = settings ?? [];

        var id = Guid.NewGuid().ToString("N");
        _dbPath = Path.Combine(Path.GetTempPath(), $"td-hosted-{id}.db");
        _hangfirePath = Path.Combine(Path.GetTempPath(), $"td-hosted-hf-{id}.db");
    }

    /// <summary>The SQLite file the hosted app writes to.</summary>
    public string DatabasePath => _dbPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);

        // UseSetting lands in the host configuration the WebApplicationBuilder is created
        // from, so Program.cs sees these even where it reads configuration eagerly during
        // service registration (the telemetry endpoint). ConfigureAppConfiguration would be
        // layered in too late for that.
        builder.UseSetting("ConnectionStrings:TouchDown", $"Data Source={_dbPath}");
        builder.UseSetting("ConnectionStrings:Hangfire", _hangfirePath);
        foreach (var (key, value) in _settings)
            builder.UseSetting(key, value);

        if (_testServices is not null)
            builder.ConfigureTestServices(_testServices);
    }

    /// <summary>
    /// Opens a SignalR client to the hosted hub through the in-memory server, exactly as the
    /// monitor page does (a .NET <see cref="HubConnection"/> with the default JSON protocol),
    /// and joins the given drive's group.
    /// </summary>
    public async Task<HubConnection> ConnectToDriveAsync(string driveId, CancellationToken ct = default)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, "/agentHub"), options =>
            {
                // The TestServer handler carries HTTP only; long polling needs nothing else.
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await connection.StartAsync(ct);
        await connection.InvokeAsync("JoinDrive", driveId, ct);
        return connection;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var baseName in new[] { _dbPath, _hangfirePath })
            foreach (var path in new[] { baseName, baseName + "-wal", baseName + "-shm" })
                try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
    }
}
