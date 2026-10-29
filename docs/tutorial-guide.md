# Configurable first-login tutorial

Open **Tutorial editor** from the editor hub or the content editors menu.
The server uses the built-in nine-step EN/FR/ES guide when `resources/tutorial.json`
does not exist. Saving creates that server-side file atomically. No database
migration or client resource copying is needed; progress uses the existing player
variable storage with reserved ID `c1026029-0000-4000-8000-999999999999`.

## Authoring

Select a step, edit its EN/FR/ES tabs, select its objective, and reorder it with
Move up/down. English is the source for the content translation catalog; unsupported
locales fall back to English. Existing translation entries remain editable. Saving
rejects missing translations, incompatible placeholders, duplicate step IDs,
invalid references and edits based on an outdated server revision. Reopen after
a version conflict to load the latest server draft.

Objectives are Read (Next), Move (server observes a position change), ReachMap
(server map ID), and Variable (player variable integer value). Boolean variables
can use 1/0. The latter three advance automatically once the server confirms the
condition and the player is free of a blocking event or activity. They can be used
to teach mini-games and naval play through existing common events that set player
variables on actual completion. Do not grant rewards for pressing Next.

The default guide introduces the interface, equipment/ammunition, combat/quests,
professions/campfires, mini-games and naval play. Only its movement step checks an
action; the other default steps are informational. Configure real activity
objectives with the map and variable IDs of your world.

## Cinematics

The cinematic dropdown lists existing common events, including scenes compiled by
Royal Cinematic Director. Select one to expose a **Play cinematic** button for that
step. The server launches the configured event through the existing gated menu
Event path; the client cannot request an arbitrary common event ID. No replacement
cinematic engine is introduced. The tutorial window hides during active cinematic
presentation, preserving native interactions and visual-skip rules. Playing a
scene does not itself complete a tutorial step. For a required scene interaction,
set a player variable in that scene and use Variable as the tutorial objective.
Default steps deliberately have no scene references, so the guide works with any
world without guessing event IDs.

## Progress and rollout

The guide starts after login once the player is available. Pause persists across
sessions. The Tutorial button under Game options > Interface resumes the guide or opens its completion screen,
which offers Replay. Completed step IDs survive reordering and reconnections. New
content is not automatically forced on characters that finished the guide. Restore
default changes the editor draft, not player progress. Disabling the guide hides
its options button; disabling first-login start leaves it available on demand. The
initial rollout also offers the guide to existing characters without progress.
Configuration edits reach online players on their next tutorial update.

Client, server and editor must be rebuilt together because packets are added to
the protocol. No live server changes are performed by this branch.

## Verification

- `dotnet test Intersect.Tests/Intersect.Tests.csproj -c Debug --filter FullyQualifiedName~TutorialTests`
- Build server, client and editor in Debug (editor requires Windows targeting).
- In game: log in with a fresh character; read the welcome; move; pause; reconnect;
  resume; complete; reconnect without automatic replay; replay from Game options > Interface.
- Editor: reorder and translate steps; save and reopen; verify disabled/autostart
  options; try missing translations and save conflicts in two editor sessions.
- Link an existing interactive cinematic; confirm presentation hides the guide,
  native choices still work and scene completion alone does not advance a Variable
  step. Set the configured variable at actual completion and verify advancement.
- Confirm ReachMap/Variable steps cannot be skipped by pressing Next or replaying
  an old request, and another character has independent progress.
