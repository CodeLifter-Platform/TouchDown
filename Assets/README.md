# TouchDown — App Icon Assets

The **The Play** mark in TouchDown's accent (`#ff7a45`) on a dark, softly glowing
tile. Full cross-platform implementation, all generated from the vector masters in `svg/`.

## Layout

```
svg/                     Vector masters (edit these; rasters derive from them)
  touchdown-icon-dark.svg     Hero app tile: dark gradient + glow + colored mark
  touchdown-icon-white.svg    Tile on white (deep mark) for light contexts
  touchdown-mark-color.svg    Bare mark, accent, transparent
  touchdown-mark-white.svg    Bare mark, white knockout (dark surfaces / tinting)
  touchdown-mark-black.svg    Bare mark, near-black (mono / light surfaces)
ios/AppIcon.appiconset/  Drop into an Xcode asset catalog (Contents.json included)
macos/
  AppIcon.iconset/       Standard iconset (Contents.json included)
  AppIcon.icns           Prebuilt .icns, use directly
android/
  mipmap-*/              Legacy launcher, square + round, 48-192 px
  adaptive/              Adaptive layers: foreground / background / monochrome, 432 px
  notification/          Status-bar icons (white), 24-96 px, drawable-*
  playstore-icon-512.png Play Store listing icon
windows/
  touchdown.ico              Multi-resolution app icon (16-256)
  tiles/                 MSIX/UWP tiles (Square 44/71/150/310, Wide 310x150, StoreLogo)
  tray/                  System tray icons, 16/24/32, white + color
web/
  favicon.ico            16/32/48 multi-res
  favicon-16/32/48.png   Individual PNG favicons
  touchdown-favicon.svg      Scalable favicon
  apple-touch-icon-180.png
  icon-192.png / icon-512.png / maskable-512.png   PWA icons
  touchdown-manifest.webmanifest  Web app manifest (theme #ff7a45)
  og-image-1200x630.png  Social / Open Graph card
```

## Notes

- **Color rule.** Tile is dark with a `#ff7a45` mark at every size. Monochrome slots
  (Android adaptive monochrome, notification, tray-white) use a single flat color as the
  platform requires (white for status bar / themed tinting).
- **Filenames.** Retina files use `-2x`/`-3x` (not `@2x`). Xcode reads the `filename`
  fields in `Contents.json`, so both catalogs import as-is. To run `iconutil -c icns`
  yourself, rename `-2x` -> `@2x` first, or just use the provided `AppIcon.icns`.
- **Maskable / adaptive** layers keep the mark inside the platform safe zone.
- **Regenerating.** Everything derives from `svg/touchdown-icon-dark.svg`. Change a master
  and re-export, or ask and I'll regenerate the suite.
