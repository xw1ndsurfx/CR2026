# Exportation — integration stage

**Status: draft, NOT PLAYABLE yet.** This branch is a foundation, not a released feature.

## Implemented in code

- `ExportationRoute` and `ExportationConfiguration` define distinct source/destination maps, two NPCs, an authored quest, required profession level, per-good quantities and rewards.
- `ExportationRules` guards packing, unique cargo seals, matching boat ID at embark and docking, one-time delivery, loss on death or sinking, and a retry at the original merchant.
- `ExportationWindow` uses the **approved** pixel-art background and individual sprite files. It draws the requested goods, packed quantities, crate contents, route summary, level and progress. Current controls are **click-to-pack** and click-to-remove; drag/drop is still pending. Its callbacks intentionally await authoritative server changes, with no client-side award.
- `ExportationAudio` plays one-shot WAV cues for pack/remove, complete order, seal, loss, delivery, open and close.
- `FrmExportationAssets` is accessible via Mini-Game Editors / Editor Hub: previews all 13 PNGs, plays all 10 WAVs and clearly marks itself preview-only.
- EN/FR/ES entries in the existing GameTextCatalog pattern.
- `Shared/ExportationAssets.props` is imported by client and editor projects to copy PNGs and WAVs into both build and publish outputs.
- NUnit rules tests were added, but not run in the current environment.

## Install approved assets in the repository

Download `Corps_Royaux_Exportation_GFX_Sons.zip` from the ChatGPT attachment and run from the repository root:

```sh
python scripts/exportation/install_assets.py "C:/Users/you/Downloads/Corps_Royaux_Exportation_GFX_Sons.zip" --check
python scripts/exportation/install_assets.py "C:/Users/you/Downloads/Corps_Royaux_Exportation_GFX_Sons.zip"
```

The installer is standard-library-only, reads 23 explicitly allowlisted ZIP entries, verifies SHA-256 and asset types/dimensions, refuses bad packs, and writes only to:
- `resources/gui/exportation/background.png`
- `resources/gui/exportation/sprites/*.png`
- `resources/sounds/exportation/*.wav`

The GitHub connector cannot directly upload binary files from the ChatGPT filesystem in this session, so the **23 binary assets are not yet part of the PR**. Build/publish includes them once installed in a local checkout; to make CI/public builds reproducible they must be checked in to Git or distributed through an approved asset channel.

## Outstanding, do not merge as playable

- Wire event-command open, server-authoritative packets, session/replay protection, and persisted player `ExportationRun` state.
- Link source merchant to quest activation on sealing; recipient to completion; revoke/restart on failure. Transactional state + economy + XP persistence and proper rollback are necessary.
- Hook verified naval embark/dock and death/sinking notifications; prevent teleport shortcuts.
- Add the actual Exportation profession XP store and route configuration editor (current editor only previews resources).
- Finish drag-and-drop and gameplay integration tests; run a real .NET 8 client/editor/server build and test suite.
