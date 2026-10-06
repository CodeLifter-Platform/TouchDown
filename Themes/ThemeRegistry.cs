using MudBlazor;

namespace TD.Themes;

public static class ThemeRegistry
{
    public static readonly Dictionary<string, MudTheme> All = new()
    {
        ["TouchDown"] = TouchDownTheme.Theme,
        ["Yeti"] = YetiTheme.Theme,
        ["Frost"] = FrostTheme.Theme,
        ["Silver Linen"] = SilverLinenTheme.Theme,
        ["Cloud Nine"] = CloudNineTheme.Theme,
        ["Pebble"] = PebbleTheme.Theme,
        ["Midnight Ember"] = MidnightEmberTheme.Theme,
    };

    public static MudTheme Default => TouchDownTheme.Theme;

    /// <summary>The default theme's registry name.</summary>
    public const string DefaultName = "TouchDown";

    /// <summary>
    /// The theme a stored name refers to. Null, blank, and unknown names (a preferences file
    /// written by a newer build, say) all resolve to the default rather than failing.
    /// </summary>
    public static MudTheme Resolve(string? name) =>
        !string.IsNullOrWhiteSpace(name) && All.TryGetValue(name, out var theme) ? theme : Default;

    /// <summary>The registry name a stored name resolves to, for selectors.</summary>
    public static string ResolveName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && All.ContainsKey(name) ? name : DefaultName;
}
