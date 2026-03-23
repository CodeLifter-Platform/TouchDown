using Serilog;
using TD.Models;
using TD.Services;

namespace TD.Areas.Home.Index;

public interface IHomeIndexService
{
    Task<List<Drive>> GetRecentDrivesAsync(int count = 10);
    Task<ClaudeHealthStatus> GetHealthStatusAsync();
}

public class HomeIndexServiceException : Exception
{
    public HomeIndexServiceException() { }
    public HomeIndexServiceException(string message) : base(message) { }
    public HomeIndexServiceException(string message, Exception innerException) : base(message, innerException) { }
}

public class HomeIndexService : IHomeIndexService
{
    private readonly IHomeIndexServiceDA _da;
    private readonly IClaudeHealthCheck _healthCheck;
    private readonly Serilog.ILogger _log = Log.ForContext<HomeIndexService>();

    public HomeIndexService(IHomeIndexServiceDA da, IClaudeHealthCheck healthCheck)
    {
        _da = da;
        _healthCheck = healthCheck;
    }

    public async Task<List<Drive>> GetRecentDrivesAsync(int count = 10)
    {
        _log.Debug("Getting recent drives");
        try
        {
            return await _da.GetRecentDrivesAsync(count);
        }
        catch (Exception ex) when (ex is not HomeIndexServiceException)
        {
            _log.Error(ex, "Failed to get recent drives");
            throw new HomeIndexServiceException("Failed to get recent drives", ex);
        }
    }

    public async Task<ClaudeHealthStatus> GetHealthStatusAsync()
    {
        _log.Debug("Checking Claude health status");
        try
        {
            return _healthCheck.LastStatus ?? await _healthCheck.CheckAsync();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to check Claude health status");
            throw new HomeIndexServiceException("Failed to check Claude health status", ex);
        }
    }
}
