# Animation visuals, appearance windows and potion progression

## Animation Editor

Each Lower and Upper layer now has signed **Offset X (pixels)** and **Offset Y (pixels)** controls, plus **Transparency (%)**. Positive X moves right and positive Y moves down. Transparency 0 keeps the animation opaque; 90 leaves 10% visible; 100 hides its pixels. Scale, offsets and transparency preview immediately and follow the layer when swapping Lower/Upper.

Existing animations default to zero offsets and zero transparency. The server applies an additive schema migration for SQLite and MySQL with those defaults. Rebuild and deploy matching server, client and Game Editor. Existing light positions follow the authored offsets; light intensity remains independently editable.

Spell and entity-attached animations now use the visible character/NPC center without the obsolete extra 32-pixel vertical shift. Event-specific offsets and command-specific offsets still add to the animation's own offsets. Event animations retain their foreground pass. Map-only effects keep their legacy tile anchor. World Map markers also respect transparency; legend previews stay centered for readability.

## Character appearance

Character Creation and Royal Stylist share an 860×640 native window. Royal Stylist no longer contains an additional artist outer frame. Character Creation explicitly places all name/class/appearance/gender controls and action buttons after loading UI overrides, preventing older small layouts from clipping new controls.

## Royal Alchemy

Mouse input now forwards the actual pressed button through the canvas: right click rotates, left click drops. The regression test uses the real Canvas/input-handler route, not just direct calls to the board.

A successfully delivered potion reward also grants its configured completion XP to the Alchemist profession, in addition to mini-game progression. The existing reward-granted gate prevents replay requests from awarding the recipe again. Inventory/bank failure leaves the reward pending and does not grant profession XP.

Existing professions named Alchemist/Alchemy, Alchimiste/Alchimie or Alquimista are detected automatically. For a custom profession, select **Receive profession XP from completed potion recipes** in Profession Editor. Only one profession can be selected; an explicit selection takes priority over the name fallback. Normal profession XP notifications, progression caps, level rewards and persistence remain in use.
