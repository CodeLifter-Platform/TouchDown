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
}
