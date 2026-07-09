using System.Text.Json;
using TD.Models;

namespace TD.Services;

/// <summary>
/// Loads and saves <see cref="UserPreferences"/> as a JSON file stored at
/// <c>~/.config/touchdown/preferences.json</c>.
/// Registered as a singleton so all components share the same in-memory instance.
/// </summary>
public class UserPreferencesService : IUserPreferencesService
{
    private readonly ILogger<UserPreferencesService> _logger;
    private readonly string _filePath;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public UserPreferences Current { get; private set; } = new();

    public UserPreferencesService(ILogger<UserPreferencesService> logger)
    {
        _logger = logger;

        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "touchdown");

        Directory.CreateDirectory(configDir);
        _filePath = Path.Combine(configDir, "preferences.json");

        Load();
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, JsonOpts);
            await File.WriteAllTextAsync(_filePath, json, ct);
            _logger.LogDebug("Preferences saved to {Path}", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save preferences to {Path}", _filePath);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogDebug("No preferences file found at {Path}; using defaults", _filePath);
                return;
            }

            var json = File.ReadAllText(_filePath);
            var prefs = JsonSerializer.Deserialize<UserPreferences>(json, JsonOpts);
            if (prefs != null)
            {
                Current = prefs;
                _logger.LogDebug("Preferences loaded from {Path}", _filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load preferences from {Path}; using defaults", _filePath);
        }
    }
}

