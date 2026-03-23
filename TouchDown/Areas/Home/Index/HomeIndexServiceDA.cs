using Microsoft.EntityFrameworkCore;
using Serilog;
using TD.Data;
using TD.Models;

namespace TD.Areas.Home.Index;

public interface IHomeIndexServiceDA
{
    Task<List<Drive>> GetRecentDrivesAsync(int count = 10);
}

public class HomeIndexServiceDAException : Exception
{
    public HomeIndexServiceDAException() { }
    public HomeIndexServiceDAException(string message) : base(message) { }
    public HomeIndexServiceDAException(string message, Exception innerException) : base(message, innerException) { }
}

public class HomeIndexServiceDA : IHomeIndexServiceDA
{
    private readonly IDbContextFactory<TDDbContext> _dbFactory;
    private readonly Serilog.ILogger _log = Log.ForContext<HomeIndexServiceDA>();

    public HomeIndexServiceDA(IDbContextFactory<TDDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Drive>> GetRecentDrivesAsync(int count = 10)
    {
        _log.Debug("Fetching {Count} recent drives", count);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Drives
                .Include(d => d.AgentTeam)
                .OrderByDescending(d => d.CreatedAt)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to fetch recent drives");
            throw new HomeIndexServiceDAException("Failed to fetch recent drives", ex);
        }
    }
}
