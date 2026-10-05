using Microsoft.Extensions.Logging.Abstractions;
using TD.Services;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.Services;

/// <summary>
/// Preferences are a JSON file in the user's profile. A missing, corrupt or unwritable file
/// must never stop the app from launching; the worst case is defaults.
/// </summary>
public class UserPreferencesServiceTests : IDisposable
{
    private readonly TempRepo _dir = TempRepo.CreateEmptyDirectory();

    private string PathFor(string name) => Path.Combine(_dir.Path, name);

    private static UserPreferencesService Service(string path) =>
        new(NullLogger<UserPreferencesService>.Instance, path);

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task Saved_preferences_are_what_the_next_launch_loads()
    {
        var path = PathFor("preferences.json");
        var first = Service(path);
        first.Current.ThemeName = "Frost";
        first.Current.DarkMode = false;
        first.Current.TelemetryConsented = true;
        first.Current.HasRespondedToTelemetryConsent = true;
        first.Current.ConsentTimestamp = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        first.Current.ConsentVersion = "1.1";

        await first.SaveAsync();
        var second = Service(path);

        Assert.Equal("Frost", second.Current.ThemeName);
        Assert.False(second.Current.DarkMode);
        Assert.True(second.Current.TelemetryConsented);
        Assert.True(second.Current.HasRespondedToTelemetryConsent);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero), second.Current.ConsentTimestamp);
        Assert.Equal("1.1", second.Current.ConsentVersion);
    }

    [Fact]
    public void A_missing_file_means_defaults_and_the_directory_is_created_for_the_first_save()
    {
        var path = Path.Combine(_dir.Path, "nested", "deeper", "preferences.json");

        var service = Service(path);

        Assert.True(service.Current.DarkMode);
        Assert.Null(service.Current.ThemeName);
        Assert.False(service.Current.TelemetryConsented);
        Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Fact]
    public void A_corrupt_file_falls_back_to_defaults_instead_of_blocking_the_launch()
    {
        var path = PathFor("preferences.json");
        File.WriteAllText(path, "{ this is not json");

        var service = Service(path);

        Assert.True(service.Current.DarkMode);
        Assert.False(service.Current.TelemetryConsented);
    }

    [Fact]
    public async Task A_corrupt_file_is_replaced_by_the_next_save()
    {
        var path = PathFor("preferences.json");
        File.WriteAllText(path, "garbage");
        var service = Service(path);
        service.Current.ThemeName = "Yeti";

        await service.SaveAsync();

        Assert.Equal("Yeti", Service(path).Current.ThemeName);
    }

    [Fact]
    public void A_file_from_a_build_that_knew_fewer_fields_still_loads()
    {
        var path = PathFor("preferences.json");
        File.WriteAllText(path, """{"telemetryConsented":true,"hasRespondedToTelemetryConsent":true,"consentVersion":"1.0"}""");

        var service = Service(path);

        Assert.True(service.Current.TelemetryConsented);
        Assert.True(service.Current.DarkMode);
        Assert.Null(service.Current.ThemeName);
    }

    [Fact]
    public async Task An_unwritable_location_makes_saving_a_logged_failure_not_a_crash()
    {
        // A directory where the file should be: the write fails, the app carries on.
        var path = PathFor("preferences.json");
        Directory.CreateDirectory(path);
        var service = Service(path);
        service.Current.ThemeName = "Pebble";

        await service.SaveAsync();

        Assert.Equal("Pebble", service.Current.ThemeName);
    }

    [Fact]
    public void The_default_location_is_under_the_profile_config_directory()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "touchdown", "preferences.json");

        Assert.Equal(expected, UserPreferencesService.DefaultFilePath());
    }
}
