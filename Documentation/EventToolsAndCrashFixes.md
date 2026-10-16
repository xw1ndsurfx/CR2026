# Event tools, housing interactions and crash fixes (2026-10-16)

## Confirmed crashes

- Editor copy/paste: the 07:12:50 editor exception was a missing command-list ID while printing a nested Create Guild command. CreateGuildCommand lacked branch-ID remapping. Pasted guild branches now receive independent IDs; collection traverses shared branches once. The regression test invokes the actual copy/paste buttons and command printer repeatedly on nested conditional/guild/disband commands.
- Client: Windows Application Error recorded `0xc00000fd` at 05:32:37. Local dump `Intersect Client.exe.1044.dmp` confirms System.StackOverflowException and 4,990 repeated RichLabel.SplitLabel frames among the first 5,000 frames examined. Rich text wrapping is now iterative, consumes text on every pass, respects padding, preserves Unicode text elements and defers layout until positive content width exists. Tests cover leading spaces, long words, tiny widths and resizing, plus the existing quest scrolling tests. The dump was inspected locally with Microsoft ClrMD; it was not uploaded.
- The separate Steam initialization NullReferenceException was caught and followed by successful login in the logs. Standalone clients now skip Steam calls when the optional native library or Steam initialization is unavailable. This is independent of the fatal text-layout recursion.
- An older 05:56 Windows report also names uxtheme.dll in an editor stack overflow. That native report is separate from the confirmed 07:12 clipboard exception; this change does not claim to identify its native call chain.

## Editor navigation and EXP schedules

- **Events → Experience Events**: enabled flag, event name, weekly days, From hour/minute, Until hour/minute and bonus percentage. Times use the server's local timezone. An earlier Until time crosses midnight; equal From/Until means 24 hours. The interval includes the start and excludes the end. Existing saved durations are preserved until the end time is edited.
- **System Editor → Combat Music**: music file and grace period only. Invasion music retains priority.
- **System Editor → Bank Settings**: existing account requirements remain editable independently.
- Saves merge only the relevant section under the server configuration lock. Two open editors cannot accidentally replace settings from the other system.

## Marketplace and dialogues

Marketplace searches source/translated item names and seller names, ignoring case. The count shows matching/total listings; refresh preserves the search. The server already supplies all active listings, so the filter covers the complete received market.

Dialogue portraits keep their aspect ratio and fit within a maximum 96-pixel box above the prompt. Large face images no longer use their full source dimensions over the dialogue text.

## Furniture

The Housing Editor catalog has **Select graphic...**, reusing the Event Graphic tileset selector. Click/drag a rectangle of 1–8 tiles per dimension. The optional **On interaction** dropdown selects an existing common event; None keeps furniture decorative.

Owners and visitors can click interactive furniture in the house, or use **Placed furniture → Use**. The server verifies the room instance, public access for visitors, the placement, proximity (within two tiles of its anchor), event permission, player availability and a short cooldown. Normal common-event page conditions still apply. For a bed, create a common event that heals the player, then assign it to the furniture. Changing the catalog's common-event assignment also updates existing copies of that furniture; expired monthly offers can keep their definitions in the catalog.

## Resource brush

In Map Layers → Attributes → Resource, select the resource and enable **Resource brush**. Radius is 0–8 tiles and density is 1–100%. Click or drag with the normal painting tool. Existing non-walkable attributes and event tiles are preserved. Each cell is sampled once per stroke, avoiding density accumulation when the cursor stays still. Normal map Undo restores the stroke; flood fill and other attribute tools retain their existing behavior.

## Chat emotes

Open a common event and check **Allow this event in the chat emote menu**, then save. Enable it on the desired [SHOUTBOX] events; other common events remain unavailable to clients by default. The small **Emotes** dropdown sits just above the chat without changing the chat layout. Its list refreshes when opened. Selecting a permitted entry executes the common event with its normal page conditions. Automatic initial dropdown selection never triggers an emote. The server rechecks the opt-in and throttles execution; a client cannot request arbitrary common events.

## Validation and rollout

Build client, editor and server together, then restart them. No artwork, maps, player inventories or deployed binaries are replaced by this source update. New player-facing UI strings are provided in English, French and Spanish. Housing and emote settings use existing JSON persistence without new database columns.
