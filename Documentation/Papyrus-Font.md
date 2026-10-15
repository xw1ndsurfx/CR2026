# Papyrus font test for Corps Royaux

This branch provides a local installer for converted Papyrus MonoGame/XNB font files.

The uploaded conversion pack contains Papyrus Regular XNB files for sizes **8 through 26**.

## Install

From the CR2026 repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-papyrus-font.ps1 -ZipPath "C:\path\to\Papyrus.zip" -ResourcesDirectory "C:\path\to\CURRENT CLIENT & EDITOR\resources"
```

The installer copies the converted XNB files into `resources/fonts` using the filenames expected by Intersect:

- `Papyrus_12_Regular.xnb` -> `papyrus_12.xnb`
- `Papyrus_14_Regular.xnb` -> `papyrus_14.xnb`
- etc.

It also updates the client config and creates a timestamped backup first.

Default test sizes:

- `GameFont`: `papyrus,12`
- `UIFont`: `papyrus,14`
- `EntityNameFont`: `papyrus,12`
- `ChatBubbleFont`: `papyrus,12`
- `ActionMsgFont`: `papyrus,12`

These can be overridden from the command line, for example:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-papyrus-font.ps1 `
  -ZipPath "C:\path\to\Papyrus.zip" `
  -ResourcesDirectory "C:\path\to\CURRENT CLIENT & EDITOR\resources" `
  -GameSize 14 `
  -UiSize 16
```

The converted Papyrus font binaries remain local and are not committed to the public CR2026 repository.
