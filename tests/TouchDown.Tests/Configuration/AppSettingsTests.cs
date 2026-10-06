using Microsoft.Extensions.Configuration;
using Serilog;

namespace TouchDown.Tests.Configuration;

/// <summary>
/// appsettings.json drives logging and the database location. Both were previously
/// unconfigured — Serilog had no sinks (so every Log.ForContext call vanished) and the
/// SQLite path was hardcoded (so container data landed outside the mounted volume).
/// </summary>
public class AppSettingsTests
{
    private static string AppSettingsPath()
    {
        // Walk up from the test binary to the repository root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TouchDown.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "TouchDown", "appsettings.json");
    }

    private static IConfigurationRoot LoadAppSettings() =>
        new ConfigurationBuilder().AddJsonFile(AppSettingsPath()).Build();

    [Fact]
    public void AppSettings_DefinesAConnectionString()
    {
        var config = LoadAppSettings();

        Assert.False(string.IsNullOrWhiteSpace(config.GetConnectionString("TouchDown")));
    }

    [Fact]
    public void ConnectionString_CanBeOverriddenByEnvironmentConvention()
    {
        // This is how the Dockerfile points the database at the mounted volume.
        var config = new ConfigurationBuilder()
            .AddJsonFile(AppSettingsPath())
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TouchDown"] = "Data Source=/app/data/touchdown.db"
            })
            .Build();

        Assert.Equal("Data Source=/app/data/touchdown.db", config.GetConnectionString("TouchDown"));
    }

    [Fact]
    public void SerilogSection_ProducesAUsableLogger()
    {
        // ReadFrom.Configuration throws on an unresolvable sink or malformed section,
        // so a bad settings file fails here rather than silently logging nowhere.
        var config = LoadAppSettings();

        using var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .CreateLogger();

        Assert.NotNull(logger);
        logger.Information("smoke test {Value}", 42);
    }

    [Fact]
    public void SerilogSection_ConfiguresAtLeastOneSink()
    {
        var config = LoadAppSettings();

        var sinks = config.GetSection("Serilog:WriteTo").GetChildren().ToList();

        Assert.NotEmpty(sinks);
        Assert.Contains(sinks, s => s["Name"] == "Console");
    }

    [Fact]
    public void DockerfilePointsTheDatabaseAtTheMountedVolume()
    {
        // Regression: compose mounts /app/data but the app wrote to the working directory,
        // so every container recreate lost the database.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TouchDown.sln")))
            dir = dir.Parent;

        var dockerfile = File.ReadAllText(Path.Combine(dir!.FullName, "Dockerfile"));

        Assert.Contains("ConnectionStrings__TouchDown", dockerfile);
        Assert.Contains("/app/data/touchdown.db", dockerfile);
    }
}
