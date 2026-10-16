# Sound / Voice Sound Import

Open **Game Editors > Sound / Voice Sound Import**. Every file/ZIP import asks which type to use:

- **Sound (effects)**: staged in `soundimport/sounds/<subcategory>`, exported to `resources/sounds`.
- **Voice Sound (dialogue)**: staged in `soundimport/voicesound/<subcategory>`, exported to `resources/voicesound`.

The Library filter shows Sound, Voice Sound or All audio. Each row and preview shows its type. Subcategories such as Combat, Voice or Ambient are separate from this explicit audio type; filename classification never overrides the chosen type.

Use **IMPORT SELECTED TO GAME** or **IMPORT ALL FILTERED TO GAME** to export staged files. **Import as** and **Game folder** show the destination. Selecting a type or file defaults the target to that file's type; the target can then be changed explicitly when correcting an older staged file. **OPEN DESTINATION** opens the selected game folder.

Imports still produce 16-bit PCM WAV, preserve source files and prefix duplicate names (`2_name.wav`, `3_name.wav`, etc.). Duplicate checks are independent in the two game folders. Older untyped `soundimport/<subcategory>` libraries remain accessible as Sound; their original files are preserved.

In Event Editor's Play Sound command, **Voice Sound** lists only `resources/voicesound`; ordinary Sound selectors list only `resources/sounds`. Voice files remain controlled by the player's Voice Sound setting. Include the exported audio folders when distributing resources to clients.
