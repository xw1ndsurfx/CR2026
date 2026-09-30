# Empire 7 font for Corps Royaux

Corps Royaux can use **Empire 7 by Ivano Palmentieri / somepx** without storing the licensed font binaries in the public CR2026 repository.

Font page and license terms: https://somepx.itch.io/pixel-font-empire

## Current workflow

Use the external font-conversion tool to convert Empire 7 to MonoGame/XNB first. The converted package should contain files named like:

- `Empire 7_8_Regular.xnb`
- `Empire 7_9_Regular.xnb`
- ...
- `Empire 7_26_Regular.xnb`

The CR installer then imports those ready-made XNB files and renames them to the naming convention expected by Intersect:

- `Empire 7_16_Regular.xnb` -> `resources/fonts/empire7_16.xnb`
- `Empire 7_20_Regular.xnb` -> `resources/fonts/empire7_20.xnb`

Intersect discovers font families from the part of the XNB filename before the final size separator, so these names register the family as `empire7`.

## Install

From the CR2026 repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-empire-font.ps1 -ZipPath "C:\path\to\Empire 7.zip" -ResourcesDirectory "C:\path\to\CURRENT CLIENT & EDITOR\resources"
```

The script:

1. extracts the converted ZIP to a temporary folder;
2. finds all `Empire 7_<size>_Regular.xnb` files;
3. copies them to `resources/fonts` as `empire7_<size>.xnb`;
4. overwrites the older locally generated Empire 7 XNB files if present;
5. updates the five client font settings;
6. creates a timestamped backup of `config.json`.

Recommended defaults are:

- `GameFont`: `empire7,16`
- `UIFont`: `empire7,20`
- `EntityNameFont`: `empire7,16`
- `ChatBubbleFont`: `empire7,16`
- `ActionMsgFont`: `empire7,16`

If the config already has an explicit `empire7,<size>` value and that size exists in the converted pack, the installer preserves it.

## Why this replaces the custom MGCB pipeline

The previous experimental branch attempted to build a custom MonoGame bitmap-font content processor. That added unnecessary build dependencies and failed on the local machine before the processor could be built.

The external converter already produces ready-to-load XNB SpriteFonts, so CR does not need to compile the licensed font itself. Importing and renaming the converted XNB files is simpler and avoids changing the game renderer or content pipeline.

## Licensing

The public repository contains only the installer/documentation. It does **not** contain the original TTF, PNG, or converted Empire 7 XNB files. Those remain local.
