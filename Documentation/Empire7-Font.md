# Empire 7 bitmap font for Corps Royaux

This branch loads **Empire 7 by Ivano Palmentieri / somepx** directly from the original bitmap sheet shipped with the font.

## Why this exists

The normal Intersect font converter works correctly. The Papyrus test confirmed that converted XNB fonts load and render normally in Corps Royaux.

Empire 7 is different because its authored pixel artwork does not visually match the TTF-rasterized SpriteFont closely enough. The client therefore bypasses TTF/XNB rasterization for this font and constructs a MonoGame `SpriteFont` at runtime from the original `empire_7.png` grid and `empire_7.txt` Construct 3 spacing data.

The source sheet is rendered through the existing Intersect `SpriteBatch` with `SamplerState.PointClamp`, and only whole-number render scaling is used. This preserves the original pixel shapes.

## Install

Use the **original** Empire 7 ZIP from somepx, not the converted XNB ZIP:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-empire-font.ps1 -ZipPath "C:\path\to\empire_7_v2.zip" -ResourcesDirectory "C:\path\to\CURRENT CLIENT & EDITOR\resources"
```

The script copies only these licensed local files into the game resources:

- `resources/fonts/empire7.bitmapfont.png`
- `resources/fonts/empire7.bitmapfont.txt`

They are not committed to CR2026.

The script also removes stale local `empire7_*.xnb` files so they cannot mask the bitmap version.

## Runtime behavior

`MonoContentManager.LoadFonts()` still loads normal XNB fonts exactly as before. It additionally discovers files named:

```text
<family>.bitmapfont.png
<family>.bitmapfont.txt
```

When both files exist, the runtime bitmap font replaces an XNB font with the same family name.

For Empire 7 the family is `empire7`.

The font uses the spacing values from the original `empire_7.txt` file and the exact 16x16 source cells from the PNG. The first unused transparent cell is used for the space glyph.

Logical sizes 8-26 remain available. They map to whole-number pixel scaling so the font remains crisp.

## Default test settings

The installer sets all five global CR font roles to `empire7,16` by default:

- GameFont
- UIFont
- EntityNameFont
- ChatBubbleFont
- ActionMsgFont

Sizes can be changed with `-GameSize`, `-UiSize`, `-EntityNameSize`, `-ChatBubbleSize`, and `-ActionMsgSize`.

## Licensing

The public repository contains only runtime support, the local installer, and documentation. The licensed PNG/TTF and locally generated resources are not committed.
