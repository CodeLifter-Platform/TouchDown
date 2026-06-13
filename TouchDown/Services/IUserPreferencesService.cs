using TD.Models;

namespace TD.Services;

public interface IUserPreferencesService
{
    /// <summary>Returns the current in-memory preferences (loaded once on startup).</summary>
    UserPreferences Current { get; }

    /// <summary>Persists the current preferences to disk.</summary>
    Task SaveAsync(CancellationToken ct = default);
}

