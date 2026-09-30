# Empire 7 font for Corps Royaux

Corps Royaux can use **Empire 7** as its client font without storing the licensed TTF in the public source repository.

## Why the font file is not committed

The Corps Royaux repository is public. The Empire 7 license permits use in games, including commercial projects within the author's stated terms, but does not permit redistributing the font in an open-source project. For that reason, neither `Empire 7.ttf` nor the generated `empire7_*.xnb` files belong in Git.

Font page and license terms: https://somepx.itch.io/pixel-font-empire

Attribution: **Empire 7 by Ivano Palmentieri (somepx)**.

## Install / migrate

From the CR2026 repository root, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-empire-font.ps1 -ZipPath "C:\path\to\empire_7_v2.zip"
```

The script:

1. extracts the supplied ZIP to a temporary directory;
2. reads the character set shipped with Empire 7;
3. creates MonoGame SpriteFont definitions for the standard Intersect sizes (8 through 26 pt);
4. builds `empire7_*.xnb` using MonoGame MGCB 3.8.2.1105;
5. copies the compiled fonts to `resources/fonts`;
6. updates `GameFont`, `UIFont`, `EntityNameFont`, `ChatBubbleFont`, and `ActionMsgFont` in `resources/config.json`, preserving the nearest existing size;
7. creates a timestamped backup of `config.json` before changing it.

If `mgcb` is already installed, it is reused. Otherwise the script installs `dotnet-mgcb` into a temporary tool directory, so the global .NET tool list is not modified.

## Custom resources directory

If the client resources live elsewhere:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-empire-font.ps1 `
  -ZipPath "C:\path\to\empire_7_v2.zip" `
  -ResourcesDirectory "C:\path\to\client\resources"
```

To generate the XNB files without touching `config.json`:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-empire-font.ps1 `
  -ZipPath "C:\path\to\empire_7_v2.zip" `
  -SkipConfigUpdate
```

## Runtime naming

Intersect discovers compiled fonts from `resources/fonts` by filename. The generated files are named like:

- `empire7_8.xnb`
- `empire7_10.xnb`
- `empire7_12.xnb`
- ...
- `empire7_26.xnb`

This registers the family as `empire7`, so a client setting such as `"UIFont": "empire7,12"` works with the existing font loader.

## Pixel-font note

The original Empire 7 page recommends 12 pt / 24 pt as especially suitable sizes and recommends avoiding anti-aliasing. The migration intentionally builds the same size range used by the existing Intersect fonts so current layouts do not suddenly change dimensions. If a specific CR window looks soft or cramped, tune that window to 12 or 24 pt rather than replacing the font files.
