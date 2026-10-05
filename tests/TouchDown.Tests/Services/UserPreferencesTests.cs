using TD.Models;
using TD.Themes;

namespace TouchDown.Tests.Services;

/// <summary>
/// Appearance used to be component-local state in MainLayout, so a reload discarded it.
/// It is now part of the persisted preferences.
/// </summary>
public class UserPreferencesTests
{
    [Fact]
    public void DefaultsMatchThePreviousBehaviour()
    {
        // Dark mode was hardcoded true and the registry default was used, so a fresh
        // install must still look the same.
        var prefs = new UserPreferences();

        Assert.True(prefs.DarkMode);
        Assert.Null(prefs.ThemeName);
    }

    [Fact]
    public void UnsetThemeFallsBackToTheRegistryDefault()
    {
        var prefs = new UserPreferences();

        var resolved = ResolveTheme(prefs.ThemeName);

        Assert.Same(ThemeRegistry.Default, resolved);
    }

    [Fact]
    public void AKnownThemeNameResolves()
    {
        foreach (var name in ThemeRegistry.All.Keys)
        {
            var resolved = ResolveTheme(name);
            Assert.Same(ThemeRegistry.All[name], resolved);
        }
    }

    [Fact]
    public void AnUnknownThemeNameFallsBackRatherThanBreaking()
    {
        // A preferences file written by a newer build could name a theme this one lacks.
        var resolved = ResolveTheme("A Theme That Was Removed");

        Assert.Same(ThemeRegistry.Default, resolved);
    }

    [Fact]
    public void ThemeAndDarkModeRoundTripThroughJson()
    {
        // Preferences are persisted as JSON on disk, so the new fields must survive it.
        var prefs = new UserPreferences { ThemeName = "Frost", DarkMode = false };

        var json = System.Text.Json.JsonSerializer.Serialize(prefs);
        var restored = System.Text.Json.JsonSerializer.Deserialize<UserPreferences>(json)!;

        Assert.Equal("Frost", restored.ThemeName);
        Assert.False(restored.DarkMode);
    }

    [Fact]
    public void OlderPreferencesFileWithoutAppearanceStillLoads()
    {
        // A file written before these fields existed must not fail to deserialize.
        const string legacy = """{"telemetryConsented":true,"hasRespondedToTelemetryConsent":true,"consentVersion":"1.0"}""";

        var restored = System.Text.Json.JsonSerializer.Deserialize<UserPreferences>(
            legacy, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.True(restored.TelemetryConsented);
        Assert.True(restored.DarkMode);   // default applies
        Assert.Null(restored.ThemeName);
    }

    /// <summary>Mirrors how MainLayout and AppearanceSettings resolve a stored theme name.</summary>
    private static MudBlazor.MudTheme ResolveTheme(string? name) =>
        !string.IsNullOrWhiteSpace(name) && ThemeRegistry.All.TryGetValue(name, out var theme)
            ? theme
            : ThemeRegistry.Default;
}
