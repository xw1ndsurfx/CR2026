# Royal Cinematic Director — architecture and delivery status

Reference baseline: `main` at `a0aaf4efc3c7552fd086787c9b3beeb913e774a7`.
The supplied final WYSIWYG image and full brief are preserved in `References/`.
Additional requirement: interactive pauses, dialogue choices and player actions during scenes.

## Reuse identified before implementation

* Editor Hub: `frmMain.EditorHub.cs`, `HubCatalog.cs` and embedded catalog; register the same menu action.
* Map viewport: `Core/Graphics.cs` already renders real map layers, autotiles, events, animations, fog and lighting. Reuse its render pass; never substitute reference artwork for the map.
* Event Editor: `FrmEvent`, `EventPage.CommandLists`, server `CommandProcessing` and `Event.Update`. Dedicated cinematic pages store editable source and compile to native commands. Existing pages and command IDs remain valid.
* Move Route: `EventMoveRoute` and `MoveRouteAction`, finite tile directions, native speed/frequency and route completion. Player routes and personal map Events are supported. Arbitrary live NPCs and shared global Events must not be moved by personal scenes.
* Animation Editor: `AnimationDescriptor`, editor `Entities.Animation`, client animation packets and entity/map offsets. Reuse existing animation IDs.
* Camera: client `Graphics.UpdateView` and renderer scale. Add recipient-only event camera commands, without changing the saved user zoom preference.
* Naval/cannon: `NavalWorld.CannonView`, `MapInstance.NavalPerspective` and server naval snapshots already project loaded map terrain and sprite billboards. This is a separate, bounded perspective pass, not a general 3D scene graph. Fleet controls must continue through server naval commands.
* Quests: `QuestDescriptor` start/end/task events and `StartCommmonEventCommand` provide narrative hooks. Rewards, conditions, choice branches and content translations stay on existing server paths.
* Audio: native sound/music catalogs and Sound Import; no Inworld, generated voices, paid service or new package dependency.

## Scheduling and safety

Timeline times are in milliseconds. Dialogue, choice, native route completion and common-event clips are barriers: subsequent timeline time resumes after the barrier. This avoids proceeding while the player is reading or a colliding route is still unfinished. Camera interpolation uses the same bounded easing math in preview and client. Scenes are map-local; no additional map or enemy visibility is requested.

Dedicated cinematic Event pages contain the source scene plus generated native commands. Regeneration replaces only the generated page, not arbitrary authored events. Native Event/Quest conditions remain authoritative. A scene cannot grant rewards through a client completion packet. Personal scene actors must be the player or non-global local Events. Paths are cardinal tile steps with static blocked-tile validation; dynamic collisions remain authoritative at runtime. Scene start anchors are checked before taking control.

FR: les temps après une interaction sont relatifs à sa fin. Une scène doit partir des positions prévues; elle ne téléporte pas silencieusement le joueur. EN: later clips resume after interactive/native barriers. ES: los clips siguientes se reanudan después de las barreras interactivas.

## Subsequent phases

Naval formations, synchronized cannon fire, battlefield groups/culling, shared scene synchronization, saga unlock rules and production templates require dedicated implementations and connected tests. Saga/chapter/sequence metadata is organizational only; it does not replace quest progression. No permanent lore, definitive royal names or fabricated game assets are introduced.
