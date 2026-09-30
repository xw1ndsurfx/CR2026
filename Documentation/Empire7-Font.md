# Empire 7 font for Corps Royaux

Corps Royaux can use **Empire 7 by Ivano Palmentieri / somepx** without storing the licensed font files in the public source repository.

Font page and license terms: https://somepx.itch.io/pixel-font-empire

## Why the v2 installer uses the PNG instead of the TTF

Empire 7 is a true pixel font. Its author recommends 12/24 pt and explicitly recommends disabling anti-aliasing. MonoGame's normal `FontDescriptionProcessor` rasterizes TTF fonts through its font pipeline, which can bake smoothed edge pixels into the generated SpriteFont atlas. Point sampling at runtime cannot remove smoothing that is already present in the atlas.

The Corps Royaux installer therefore uses the **supplied `empire_7.png` sprite sheet and spacing data** as the source of truth. It scales that bitmap only by whole-number nearest-neighbour factors and then packages the exact pixels into MonoGame SpriteFont XNB files through a small custom content processor.

This preserves the appearance of the original Empire 7 artwork much more closely than rasterizing `Empire 7.ttf` at arbitrary sizes.

## Install / rebuild

From the CR2026 repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-empire-font.ps1 -ZipPath "C:\path\to\empire_7_v2.zip" -ResourcesDirectory "C:\path\to\CURRENT CLIENT & EDITOR\resources"
```

The installer:

1. extracts the purchased ZIP to a temporary directory;
2. reads `empire_7.png` and `empire_7.txt`;
3. builds the local `EmpireBitmapFontProcessor` against the same MonoGame version as CR;
4. creates nearest-neighbour bitmap variants for the normal Intersect font slots;
5. builds `empire7_*.xnb` without TTF rasterization;
6. copies the XNB files to `resources/fonts`;
7. updates the five CR font settings and creates a timestamped backup of `config.json`.

Recommended defaults used by the installer are:

- `GameFont`: `empire7,16`
- `UIFont`: `empire7,20`
- `EntityNameFont`: `empire7,16`
- `ChatBubbleFont`: `empire7,16`
- `ActionMsgFont`: `empire7,16`

If a current setting is already larger than the recommendation, the installer keeps the larger size and picks the nearest generated slot.

## Pixel scaling

The generated slots use whole-number scaling only:

- 8-14 -> 1x source pixels
- 16-22 -> 2x source pixels
- 24-26 -> 3x source pixels

This keeps glyph edges aligned to the pixel grid. CR's renderer already uses `SamplerState.PointClamp`, so the bitmap atlas stays crisp when drawn.

## Re-running after the old TTF-based install

Run the installer again with the same purchased ZIP and the correct client `resources` directory. The new XNB files use the same `empire7_<size>.xnb` names and overwrite the previous TTF-generated files.

Restart the client after the installer finishes.

## Licensing

The public repository contains only the migration/build tooling. It does **not** contain `Empire 7.ttf`, `empire_7.png`, or generated Empire 7 XNB assets. Those files are created locally from the user's licensed ZIP.
