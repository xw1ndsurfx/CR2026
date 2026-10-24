LOGIKLIK EDITOR HUB

Base: main at 4c588f43eb63a1e1d4b075108f0c170bc67ed232 (includes PR #178).
Framework: .NET 8 Windows Forms, DarkUI, DockPanel Suite. No WPF/webview dependency.

ARCHITECTURE
- Forms/EditorHub/HubCatalog.cs: stable metadata and registration of existing leaf menu commands. The registry delegates to ToolStripMenuItem.PerformClick and watches additions/removals. Map navigation has two explicit workspace commands.
- Resources/EditorHub/Catalog.json: 53 built-in entries with localized titles/descriptions, categories, subcategories, tags, keywords and independent artwork. Add metadata with the same stable Name as the original menu command to add a tool. External tools still appear automatically and use a hash of their relative executable path as identity.
- Forms/EditorHub/EditorHubView.cs: owner-drawn WinForms navigation, banner, responsive virtual card grid, filters, actions, recents and keyboard interaction. Only visible cards are painted; decoded illustrations are cached.
- Forms/EditorHub/HubPreferences.cs: host/port-scoped user preferences via the existing Preferences mechanism. EditorHub.v1 stores favorites, 12 most recent tool IDs with UTC timestamps, and sidebar state. No game content or player settings are changed. Missing or removed tools are hidden from the visible history.
- Forms/frmMain.EditorHub.cs: reuses the historical command initialization; a sibling overlay preserves the map swap chain, docking contents and undo state. The Hub tracks docking geometry and leaves save reminders visible. Legacy menus remain available during validation.

NAVIGATION
- The Hub opens at editor startup. Its main menu entry and toolbar button reopen it.
- Ctrl+K opens/focuses search in the main workspace. The existing shortcuts were inspected; Ctrl+K was unused. It does not install a global hotkey or steal input from other modal editors.
- Search matches French, English and Spanish titles, descriptions, categories, tags and keywords, ignoring case/accents. PNJ/NPC and Objet/Item are supported. Filters combine with search.
- Arrows move the card focus; Enter opens; Space toggles the focused favorite. Escape clears search first, then returns to the existing map. Tab reaches sidebar, search, filters and card grid.
- The star on each card updates local favorites. Recent entries reopen the real editor command. History intentionally records editor access rather than inventing authored object names or unverified object IDs.
- New map uses the existing map creation procedure. New map event activates the existing Events map layer; choose a tile to create/edit the event. Other quick actions save the current map or open Common Events. New/open project commands are not invented: this engine edits the connected game's content.

ILLUSTRATIONS
53 independent 256x96 PNG compositions plus one 720x168 welcome banner are embedded with the editor. They reuse the user's existing game resources and supplied Map Editor capture, following the preference in the brief. They contain no added text. ArtSources.csv records the relative source assets and crops. scripts/Generate-EditorHubArt.ps1 reproduces the compositions given the existing resource folder and the original capture. Original game files are read only. No new image generation service was used.

INVENTORY
Inventory.csv maps the original menus, stable IDs, actual target forms/commands, categories, subcategories and illustration names. Mini-games include Poker, Blackjack, Potions, Roulette, Cooking, Smelting, Smithing and Lockpicking. Smelting/Smithing retain FrmMetalworking; the other mini-games retain their existing reward/settings navigation.

VALIDATION
Build and test commands (Windows, .NET SDK):
  dotnet build Utilities/InterfaceEditorTests/Intersect.InterfaceEditorTests.csproj -c Debug -p:RuntimeIdentifiers=win-x64 -r win-x64 -p:NuGetAudit=false
  Intersect.Tests.Editor.exe --hub-only
  Intersect.Tests.Editor.exe --windows-only
  Intersect.Tests.Editor.exe --picker-only
  Intersect.Tests.Editor.exe --editor-fixes-only
  Intersect.Tests.Editor.exe --theme-only
Set CODEX_HUB_PREVIEW to an output directory to generate captures. The test harness sizes the actual offscreen WinForms window using SetWindowPos so a smaller desktop cannot silently reduce the requested 1920x1080, 1600x900, 1366x768 and 1280x720 dimensions. Captures render the actual integrated control, not an HTML reconstruction. Repository copies use an optimized 256-color PNG palette; the before-comparison image is reduced to 640 pixels wide. The original RGB captures retain the exact requested resolutions in CODEX_HUB_PREVIEW.

REFERENCE COMPARISON
The final layout retains the dark navy palette, green selection, left navigation, landscape banner, search/filter row, four-column home grid when space allows, image/title/description/tag card structure, quick actions and real recent tools. Smaller widths use fewer columns with vertical scrolling. The reference's fictional project actions/recent object names are replaced with real supported commands and an honest empty state. Combat is represented by the existing Game Balance Lab.

LIMITS AND REVIEW NOTES
- Offline captures show an empty map workspace: no authenticated game connection is faked. Live map rendering and server-backed editor responses still require a normal connected editor session. The registry coverage and command dispatch are tested without changing server, client, network protocol, databases or game logic.
- The illustrations are original-resource compositions, not newly painted versions of the reference's chest, portraits and monsters. They share the game's actual pixel-art assets; a future art pass can replace any independent PNG.
- Native title-bar appearance follows Windows/DWM and can differ from the supplied screenshot in the test environment.
- Existing dependency and engine compiler warnings remain.
- The previous CheckedListBox-to-ComboBox preview binding in frmNpc.AutoCreator.cs is corrected as a build prerequisite; spell selection behavior is preserved.
- This branch is for review. Do not merge automatically. Installed engine executables have not been replaced.
