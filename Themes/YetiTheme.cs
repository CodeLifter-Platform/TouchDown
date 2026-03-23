/*
 * Bootswatch v5.3.3 (https://bootswatch.com)
 * Theme: yeti
 * Copyright 2012-2024 Thomas Park
 * Licensed under MIT
 * Based on Bootstrap
 */

using MudBlazor;

namespace TD.Themes;

public static class YetiTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight()
        {
            TableHover = "rgba(0,0,0,0.0392156862745098)",
            OverlayLight = "rgba(255,255,255,0.4980392156862745)",
            OverlayDark = "rgba(33,33,33,0.4980392156862745)",
            GrayDarker = "#616161",
            GrayDark = "#757575",
            GrayLighter = "#E0E0E0",
            GrayLight = "#BDBDBD",
            GrayDefault = "#9E9E9E",
            RippleOpacitySecondary = 0.2,
            RippleOpacity = 0.1,
            HoverOpacity = 0.06,
            Surface = "rgba(255,255,255,1)",
            Background = "rgba(255,255,255,1)",
            BackgroundGray = "rgba(245,245,245,1)",
            ErrorContrastText = "rgba(255,255,255,1)",
            LinesInputs = "rgba(189,189,189,1)",
            ActionDisabled = "rgba(0,0,0,0.25882352941176473)",
            TextDisabled = "rgba(0,0,0,0.3764705882352941)",
            TableStriped = "rgba(0,0,0,0.0196078431372549)",
            LinesDefault = "rgba(0,0,0,0.11764705882352941)",
            ActionDisabledBackground = "rgba(0,0,0,0.11764705882352941)",
            TableLines = "rgba(224,224,224,1)",
            Divider = "rgba(224,224,224,1)",
            DividerLight = "rgba(0,0,0,0.8)",
            Error = "rgba(244,67,54,1)",
            Tertiary = "rgba(34, 34, 34, 0.5)",
            TertiaryLighten = "rgba(34, 34, 34, 0.5)",
            TertiaryDarken = "rgba(34, 34, 34, 0.5)",
            ActionDefault = "rgba(0,0,0,0.5372549019607843)",
            DarkLighten = "rgb(87,87,87)",
            ErrorLighten = "rgb(246,96,85)",
            ErrorDarken = "rgb(242,28,13)",
            DarkDarken = "rgb(46,46,46)",
            White = "#fff",
            SecondaryLighten = "#eeeeeea7",
            WarningDarken = "#5d3a01",
            PrimaryContrastText = "#cce8f1",
            TextPrimary = "#026c8cff",
            TextSecondary = "#5f5f5f",
            TertiaryContrastText = "#f8f9fa",
            DarkContrastText = "#ccc",
            Secondary = "#eeeeeeff",
            DrawerIcon = "#eee",
            DrawerText = "#4e4d4dff",
            Warning = "#e99002",
            SecondaryDarken = "#bdbdbdff",
            InfoDarken = "#244d59",
            SuccessDarken = "#1b452a",
            Info = "#5bc0de",
            Success = "#43ac6a",
            Dark = "#333",
            DrawerBackground = "#fcfcfc",
            WarningLighten = "#fbe9cc",
            WarningContrastText = "#fbe9cc",
            SecondaryContrastText = "#4c4c4cff",
            InfoContrastText = "#def2f8",
            InfoLighten = "#def2f8",
            SuccessContrastText = "#d9eee1",
            SuccessLighten = "#d9eee1",
            PrimaryLighten = "#02b3ecff",
            PrimaryDarken = "#00384a",
            Primary = "#008cba",
            AppbarText = "#008cba",
            AppbarBackground = "#cce8f1",
            Black = "#000",
        },
        PaletteDark = new PaletteDark()
        {
            Surface = "rgba(55,55,64,1)",
            Background = "rgba(50,51,61,1)",
            BackgroundGray = "rgba(39,39,47,1)",
            ErrorContrastText = "rgba(255,255,255,1)",
            LinesInputs = "rgba(255,255,255,0.2980392156862745)",
            ActionDisabled = "rgba(255,255,255,0.25882352941176473)",
            TextDisabled = "rgba(255,255,255,0.2)",
            TableStriped = "rgba(255,255,255,0.2)",
            LinesDefault = "rgba(255,255,255,0.11764705882352941)",
            ActionDisabledBackground = "rgba(255,255,255,0.11764705882352941)",
            TableLines = "rgba(255,255,255,0.11764705882352941)",
            Divider = "rgba(255,255,255,0.11764705882352941)",
            DividerLight = "rgba(255,255,255,0.058823529411764705)",
            Error = "rgba(244,67,54,1)",
            Tertiary = "rgba(222, 226, 230, 0.5)",
            TertiaryLighten = "rgba(222, 226, 230, 0.5)",
            TertiaryDarken = "rgba(222, 226, 230, 0.5)",
            ActionDefault = "rgba(173,173,177,1)",
            DarkLighten = "rgb(56,56,67)",
            ErrorLighten = "rgb(246,96,85)",
            ErrorDarken = "rgb(242,28,13)",
            DarkDarken = "rgb(23,23,28)",
            White = "#fff",
            SecondaryLighten = "#fcfcfcff",
            WarningDarken = "#f2bc67",
            PrimaryContrastText = "#eeeeeeff",
            TextPrimary = "#eeeeeeff",
            TextSecondary = "#eeeeeeff",
            TertiaryContrastText = "#eeeeeeff",
            DarkContrastText = "#eeeeeeff",
            Secondary = "#eee",
            DrawerIcon = "#eee",
            DrawerText = "#eee",
            Warning = "#e99002",
            SecondaryDarken = "#bdbcbcff",
            InfoDarken = "#9dd9eb",
            SuccessDarken = "#8ecda6",
            Info = "#5bc0de",
            Success = "#43ac6a",
            Dark = "#333",
            DrawerBackground = "#303030",
            WarningLighten = "#2f1d00",
            WarningContrastText = "#2f1d00",
            SecondaryContrastText = "#1e1e1eff",
            InfoContrastText = "#12262c",
            InfoLighten = "#12262c",
            SuccessContrastText = "#0d2215",
            SuccessLighten = "#0d2215",
            PrimaryLighten = "#02b3edff",
            PrimaryDarken = "#015c7aff",
            Primary = "#008cba",
            AppbarText = "#008cba",
            AppbarBackground = "#001c25",
            Black = "#000",
        },
        LayoutProperties = new LayoutProperties()
        {
            AppbarHeight = "64px",
            DefaultBorderRadius = "4px",
            DrawerMiniWidthLeft = "56px",
            DrawerMiniWidthRight = "56px",
            DrawerWidthLeft = "240px",
            DrawerWidthRight = "240px",
        },
        Typography = new Typography()
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Roboto", "Helvetica", "Arial", "sans-serif"],
                FontWeight = "400",
                FontSize = ".875rem",
                LineHeight = "1.43",
                LetterSpacing = ".01071em",
                TextTransform = "none",
            },
            H1 = new H1Typography
            {
                FontWeight = "300",
                FontSize = "6rem",
                LineHeight = "1.167",
                LetterSpacing = "-.01562em",
                TextTransform = "none",
            },
            H2 = new H2Typography
            {
                FontWeight = "300",
                FontSize = "3.75rem",
                LineHeight = "1.2",
                LetterSpacing = "-.00833em",
                TextTransform = "none",
            },
            H3 = new H3Typography
            {
                FontWeight = "400",
                FontSize = "3rem",
                LineHeight = "1.167",
                LetterSpacing = "0",
                TextTransform = "none",
            },
            H4 = new H4Typography
            {
                FontWeight = "400",
                FontSize = "2.125rem",
                LineHeight = "1.235",
                LetterSpacing = ".00735em",
                TextTransform = "none",
            },
            H5 = new H5Typography
            {
                FontWeight = "400",
                FontSize = "1.5rem",
                LineHeight = "1.334",
                LetterSpacing = "0",
                TextTransform = "none",
            },
            H6 = new H6Typography
            {
                FontWeight = "500",
                FontSize = "1.25rem",
                LineHeight = "1.6",
                LetterSpacing = ".0075em",
                TextTransform = "none",
            },
            Subtitle1 = new Subtitle1Typography
            {
                FontWeight = "400",
                FontSize = "1rem",
                LineHeight = "1.75",
                LetterSpacing = ".00938em",
                TextTransform = "none",
            },
            Subtitle2 = new Subtitle2Typography
            {
                FontWeight = "500",
                FontSize = ".875rem",
                LineHeight = "1.57",
                LetterSpacing = ".00714em",
                TextTransform = "none",
            },
            Body1 = new Body1Typography
            {
                FontWeight = "400",
                FontSize = "1rem",
                LineHeight = "1.5",
                LetterSpacing = ".00938em",
                TextTransform = "none",
            },
            Body2 = new Body2Typography
            {
                FontWeight = "400",
                FontSize = ".875rem",
                LineHeight = "1.43",
                LetterSpacing = ".01071em",
                TextTransform = "none",
            },
            Button = new ButtonTypography
            {
                FontWeight = "500",
                FontSize = ".875rem",
                LineHeight = "1.75",
                LetterSpacing = ".02857em",
                TextTransform = "uppercase",
            },
            Caption = new CaptionTypography
            {
                FontWeight = "400",
                FontSize = ".75rem",
                LineHeight = "1.66",
                LetterSpacing = ".03333em",
                TextTransform = "none",
            },
            Overline = new OverlineTypography
            {
                FontWeight = "400",
                FontSize = ".75rem",
                LineHeight = "2.66",
                LetterSpacing = ".08333em",
                TextTransform = "none",
            },
        },
        ZIndex = new ZIndex()
        {
            AppBar = 1300,
            Dialog = 1400,
            Drawer = 1100,
            Popover = 1200,
            Snackbar = 1500,
            Tooltip = 1600,
        },
    };
}
