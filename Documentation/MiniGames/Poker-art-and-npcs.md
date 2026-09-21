# Poker milestone 4: playing croupier, NPCs, automatic hands and original artwork

Development branch only: feature/poker-minigame-core, draft PR 1. This document supersedes
milestone 3's human-only/manual/asset-free limitations, not its safety or testing requirements.
Read CI for the exact commit. Implementation is not proof of a successful visual playtest.

## Event settings

Start Mini-Game now adds Dealer plays and deals, Other NPC opponents, Automatic hands and
Dealing animation. Existing events keep their previous behavior: false, 0, false, None.
Example: 6 total seats, dealer enabled, 2 other NPCs, automatic hands enabled, 1000 test chips,
blinds 5/10, timeout 30 seconds. Select an existing animation or None / Aucune in the dropdown.
At least one human seat is reserved. All access events for the same map/instance/table ID
must use identical settings, including the animation GUID and NPC options. Empty the existing
table before changing its settings, or use a new Table ID on the development server.

The croupier is a server-owned opponent with its own private cards and stakes. It participates
in the same pot and remains seated. The (B) marker is the rotating hold'em betting button;
it is NOT the fixed croupier's identity. Other NPCs are marked [NPC]. The existing dealer
mechanics distribute the hand, and the selected visual effect accompanies distribution.
These are participants in the poker window, NOT new map NPC entities or sitting sprites.

NPC decisions use only their recipient-specific snapshot and public information. The initial
policy is deliberately basic, not an expert or a reproduction of TLOPO AI. NPC actions are
paced (about 1.2 seconds plus the server sweep interval). Actions pass through the same poker
state machine as human actions; bots cannot inspect a deck, human private hand or database.

Automatic tables start after a roughly five-second pause with a funded human present, then
repeat after each settled hand. The Start hand button remains available for manual starts.
Ordinary NPCs yield a full seat to a human only between hands. A full active table refuses
the new join; retry between hands. The croupier never yields its reserved seat. Joining an
available seat during a hand waits for the next deal. NPC-only tables do not run indefinitely:
the current hand settles after the last human leaves, then the registry removes the table.
A human at zero chips is not automatically refilled or entered into a new hand.

## Card art contract for the pixel artist

The client reads the files directly from resources/misc, not a cards subfolder and not gui.
No copyrighted third-party artwork or font files are included in this change.

52 face files: rank + suit + .png, for example AC.png (ace of clubs), TD.png (ten of diamonds),
QH.png (queen of hearts), KS.png (king of spades). Ranks: 2 3 4 5 6 7 8 9 T J Q K A.
Suits: C clubs/trefle, D diamonds/carreau, H hearts/coeur, S spades/pique.

One optional common back: back.png. It must be identical for all unrevealed cards.
Suggested canvas: 48 x 64 pixels, PNG with transparent corners/background. All faces and the
back should have identical dimensions. Other sizes are fitted into the UI while preserving
aspect ratio. Use T, not 10, and the exact names above. Do not overwrite unrelated assets.
Copy the files into the client resources/misc folder, then restart the client. No images are
uploaded to the server by this feature and no art-generation step is required.

Missing files fall back to existing text: [AC], [??] or [--]. Community cards, private cards,
and small seat previews use the appropriate faces/backs. Only your own or server-revealed
cards can select a face texture; unknown opponent cards never select a hidden face.

## Dealing animation

Create a normal animation in the animation editor. Its sprite sheet(s) stay in the usual
resources/animations location; the 52 card faces remain in resources/misc. Save that animation,
then select it by name in the event command. The event stores its GUID, not a dropdown index.
Deleted/missing selections are retained in the editor and safely produce no client effect.

The selected animation is rendered in the poker window, over the common-card area, when an
observed new hand or new board cards arrive. It uses the lower and upper sprite layers,
XFrames/YFrames, FrameCount and FrameSpeed. Each distribution plays one pass, capped at eight
seconds; LoopCount, sounds, map lighting, map rotations and entity effects are not applied.
It neither delays a server action nor intercepts the mouse. None disables the effect.
Refreshing, duplicate state, stale state or reopening/joining an already-active hand does not
replay the previous distribution. An all-in runout received as one snapshot plays one effect.
An actual GPU playtest with the chosen artist assets is still required.

## Currency boundary

TEST CHIPS ONLY. No Aureons, inventory objects, persistent balances or rewards are modified.
Broke NPCs are refilled only between hands so a test table remains usable. Human test re-entry
still grants the legacy test stack. This must NOT be connected to Aureons by renaming labels:
transactional buy-in, settlement, cash-out, idempotent ledger, crash recovery and a controlled
NPC bankroll must be implemented and abuse-tested first. No production deployment or merge.

## Build and tests

Update all three executables (client, server, editor) together from this branch. Back up game
data and test away from production. No database migration or deletion of JSON layouts needed.

    git fetch origin
    git switch feature/poker-minigame-core
    git pull --ff-only origin feature/poker-minigame-core
    git submodule update --init --recursive
    dotnet build Intersect.Server/Intersect.Server.csproj --configuration Debug
    dotnet build Intersect.Client/Intersect.Client.csproj --configuration Debug
    dotnet build Intersect.Editor/Intersect.Editor.csproj --configuration Debug

Tests add 12 NPC/automation groups and 5 presentation/serialization groups alongside existing
engine/registry/protocol checks. UI measurement checks remain enabled (the previous head had
a failing UI fixture; inspect the new run rather than assuming it has been resolved).

    dotnet run --project Utilities/PokerSmokeTests/Intersect.PokerSmokeTests.csproj --configuration Release
    dotnet run --project Utilities/PokerNetworkTests/Intersect.PokerNetworkTests.csproj --configuration Release
    dotnet run --project Utilities/PokerUiTests/Intersect.PokerUiTests.csproj --configuration Debug

Manual acceptance: one human plus dealer, mixed humans/NPCs, repeated hands, broken NPC stack,
zero human stack, full-table join, guest yielding, leave/disconnect/all-in settlement, actual
52 faces/back, missing assets, chosen/None/deleted animation, refresh/reopen without replay,
small-resolution scrolling and private-card separation across two real game clients.
The user's earlier screenshot confirms milestone 3's readable text and a completed hand,
not these new NPC/art/animation changes. Dependency vulnerability warnings remain outstanding.
