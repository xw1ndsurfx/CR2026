# Weapon mastery: public inspection and paid talent reset

This delivery extends the existing mastery/profile UI and native inventory/account saves.
It retains independent mastery XP and the existing weapon/equipment/paperdoll systems.

## Player flow

Character Information now has a Weapon Masteries section for online characters, including
your own. Two categories per page show level/cap, XP, percentage, XP remaining and unspent
points. View talents opens the existing mastery tree; other players' trees are read-only.
Background updates to your own XP do not replace an observed character's tree. Missing
mastery storage displays unavailable information rather than invented level/XP values.
Offline profiles still use the existing house-only view; offline mastery inspection is not added.

Your own Weapon Masteries window has Reset talents when the editor enables it. Choose a
category, review the exact price/currency and confirm. The category's ranks are removed and
points become available again through the existing XP-based calculation. XP/levels and
other weapon categories remain intact. A changed price requires a new confirmation.

Resetting requires an authenticated character outside combat, casting, archery/jousting,
trade, shops, banks, crafting and other incompatible holds/activities. Active effects from
historically mastery-owned spells must end first. Only main-inventory stacks pay the fee;
bag contents and bank holdings are excluded. Empty allocations do not incur a fee.
The service is available from the self mastery panel; dedicated trainer NPC content/quests
remain to author using the game's existing event systems.

## Editor setup

Deploy matching client/server/editor binaries to a development copy and back up its native
character database plus resources/weapon-masteries.db and royal-combat.json.

Open Weapon Mastery Editor > Mastery reset settings. Enable paid mastery resets, select the
actual existing Aureons item (or the server's intended currency item) and set the cost per
category. Save the dialog, then save the parent editor. The shared configuration draft
preserves other mastery/range/ammo edits. The currency picker reuses MiniGameCurrency's
native stackability rule, including currency items with an unchecked Stackable flag.
The server validates that the selected item exists and is compatible.

Respec defaults disabled; the initial configured price is 1000 and the allowed range is
1–100,000,000. No currency ID is invented or preselected. The price is game currency only;
no new premium purchase or item artwork is introduced.

## Payment and recovery protocol

The native character database and separate mastery SQLite database are coordinated with a
recoverable two-step operation, not a claimed cross-database atomic commit:

1. Under EntityLock and the native account save gate, stage the main-inventory debit and
   a paid-operation record in the existing Pets JSON column, then force the native account
   save. A failed save restores the live inventory/record and does not clear ranks.
2. Only after this checkpoint succeeds, clear that category's ranks and insert a permanent
   request receipt in one mastery-database transaction. A replay returns the existing
   receipt; it cannot clear later purchases or deduct another fee. Old talent-purchase
   receipts remain and cannot restore ranks cleared by a reset.
3. Reload ranks, remove historically mastery-owned native spellbook entries that no longer
   have an eligible talent grant, and save the native character with the pending record
   cleared. Other valid mastery spell grants and ordinary spells remain. Historical spell
   ownership is retained so deleting/remapping an old talent cannot free its stale spell.
   Native equipment requirements are rechecked after spell removal; currency consumption
   also updates the existing gather-item quest counters. Rank-mapped spell IDs are mastery-gated
   and ownership is tracked per spell ID; use distinct definitions for unrestricted quest abilities.

After a disconnect/restart between steps, the saved paid-operation record resumes on the
next mastery consultation, talent purchase or relevant mastery-combat check. Recovery uses
the recorded category/request even if the editor has since disabled resets or changed the
price. It never charges again. A storage failure keeps the paid record and retries with a
ten-second backoff on subsequent activity; allocation and mastery spell/passive use wait for
recovery. The UI reports the pending paid reset. XP-based level damage remains because XP
itself is preserved. There is no automatic refund of an already committed payment: its
recorded reset is completed after storage becomes available.

Storage additions: MasteryRespecReceipts in weapon-masteries.db and optional MasteryRespec
inside native Pets JSON. Existing tables/IDs and the other Pets/Jousting/Mounts fields are
preserved. Receipts are retained for replay protection; no purge tool is added in this slice.

Back up native character data and mastery data together while stopped. Before downgrading
to binaries without this feature, finish every pending paid reset or restore a coordinated
backup. Older Pets serializers may drop the unfamiliar pending-operation field when saving.

## Verification and limits

Tests exercise native inventory-slot staging, insufficient funds, failed/throwing saves,
paid-record JSON reload, rank-reset transaction/receipt replay, later purchases after reset,
category/XP isolation, public profile packet round trips and unauthenticated/foreign-target
requests. Native client controls cover read-only inspection, price confirmation changes,
unavailable profiles and public mastery paging. Native editor tests cover disabled defaults,
currency selection setup and source-draft preservation.
Recovery tests also revoke a historical mastery spell, preserve an ordinary spell and retry
the final native acknowledgement after its first save fails.

Save checkpoints are tested with deterministic callbacks plus real mastery SQLite and native
Pets serialization. This is not a multi-client production-world or forced-process-crash test
of the full authenticated account database. Full account/provider failure-injection, combat
buff/projectile, bank/trade/crafting and reconnect QA remain required before live activation.

Other outstanding mandate areas include real-client/load tests, tournament finals/rewards,
guild/spectator/reconnect modes, archive/retention tools, full cinematics and Admurin production
art, trainer tutorials/milestone quests/titles/Steam and remaining weapon/naval content.
