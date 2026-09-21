# Poker: cosmetic backs, global net wins and private celebrations

Development branch only. Temporary test chips: **no Aureons/inventory/persistent currency writes**.
Compile the client, server and editor together because packet fields/actions were appended.

## Event settings

Open Start Mini-Game / Poker. Existing events default to no global announcements and no victory animation.

- **Announce wins in GLOBAL chat**: opt-in, one message per human with positive net profit per completed hand.
- **Victory animation (winner only)**: select an existing AnimationDescriptor or None. Independent of dealing animation.
- **Dealer / NPC card back**: Classic, Royal, Pirate or Halloween. Applies to all automated opponents at this table.

Use identical settings on events accessing the same map/instance/Table ID. Recreate an idle table
(all humans leave) or use a new Table ID after editing settings. Do not deploy to production yet.

## What the amount means

The ledger captures each participant's stack **before the blinds** and compares it with the settled stack.
For example, contributing 40 and receiving 100 gives a net win of 60. An unmatched 30 returned to its owner
is not 30 in profit. A split that only returns everyone's own stakes has no positive net winner.
Multiple positive-net human winners can each be announced; NPCs are never announced globally.

Announcements are drained exactly once from a server-side completed-hand queue, before departing seats
are removed. Refresh, duplicate requests and timers do not re-announce a hand. Delivery is best effort:
a network-send exception is logged rather than automatically retried and duplicated. No durable queue
across server restarts is claimed for these volatile test tables.

Default server text: `[Poker] NAME wins AMOUNT test chips (net gain).`
Localization declaration: `Intersect.Server.Core/Localization/Strings.Poker.cs`.

## Cosmetic assets and selection

Put original images directly in the client `resources/misc`, then restart the client:

| Catalog ID | File |
| --- | --- |
| 0 | back.png |
| 1 | back_royal.png |
| 2 | back_pirate.png |
| 3 | back_halloween.png |

Keep the same dimensions/contours as the faces; 48 x 64 pixels remains the proposed authoring size.
No artist images are bundled in this change. Missing variants use back.png, then text if even the classic
back is absent. Faces keep their existing AC.png / TD.png / QH.png / KS.png naming.

At the bottom of the poker window, click **Card back** to cycle the four choices; the adjacent preview
uses the selected asset. Server confirmation is required. Both cards use the same current back. A choice
made during a live hand is queued for the next hand and cannot signal a card's hidden value.
The catalog is server-validated (not a filename, URL or upload). Choices are available equally in this test
build: no unlock shop, purchases or account-persistent cosmetics are implemented yet. Leaving the table
resets the choice; reactivating the same event without leaving preserves the seat's selection.

## Winner effect

Only the winner's recipient-specific state includes positive NetWin and the selected victory animation ID.
Other players receive neither a winner effect trigger nor another player's private cards. The client model
tracks observed completed hands, so refresh/reopen does not replay the effect and a first view of an already
finished hand is historical. The overlay is centered on the winning client's game canvas, at up to 320 x 240,
maintaining sprite-frame proportions. It does not capture mouse/keyboard input or create a map animation.

Two graphic layers, one pass capped at eight seconds. Sounds, lights and looping are not included.
The poker window must still be open when the win is observed; closing it disposes the overlay. Offline
players and players who have left are not sent a deferred celebration. Artwork and real-client visuals
still require manual acceptance testing.

## Verification

`dotnet run --project Utilities/PokerSmokeTests/Intersect.PokerSmokeTests.csproj -c Release`

`dotnet run --project Utilities/PokerNetworkTests/Intersect.PokerNetworkTests.csproj -c Release`

`dotnet run --project Utilities/PokerUiTests/Intersect.PokerUiTests.csproj -c Debug`

New tests cover net/refund/split logic, all-in departure, no duplicate notices, opt-in chat, human-only
winners, server-authorized/pinned backs, serialization, recipient isolation and victory replay protection.
The old headless UI fixture had an ambiguous Console reference; it now explicitly uses System.Console.
Existing measurement assertions remain, with the button count updated for the new cosmetic picker and
additional non-interactive overlay cleanup assertions. Synthetic text metrics are not a GPU visual test.

Manual acceptance: two clients choose different backs; change one during a hand; confirm the pending
choice only applies next hand; finish a hand; observe one global message with net profit on both clients
and an overlay only on the winner. Refresh and reopen must not repeat either notification. Repeat with
missing images/animation, a split pot and a departure/all-in. Keep server/editor/client on one commit.
