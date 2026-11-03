# Autonomous farming at player houses

Rebuild the server, client and editor together. Farming is disabled by default and uses the existing `Housing` player JSON column, so no schema migration is needed. Existing furniture, pets and house return locations remain compatible. This is a first playable implementation; a content author must assign real maps, items and artwork before enabling it.

## Configure the farm

Open **Game Editors > Housing & Furniture Catalog > Farm & hired workers**. Select a separate, isolated exterior map containing the house facade and garden. Avoid public exits, NPC spawns and gameplay rewards on this template. The exterior belongs to the same owner's shared housing instance, but uses its own map. The first version allows the owner to enter their farm; public house access does not expose farm management or its financial/storage contents.

Choose the exterior entry coordinates, salary currency and an existing entity sprite filename in `WorkerSprite`. `FieldX` and `FieldY` are the first plot's coordinates. Worker index `i`, starting at zero, uses `(FieldX + i * 2, FieldY)` for crops and the tile below for its sprite. Both tiles must be walkable and distinct from the entrance. The server validates those positions against the configured map dimensions and attributes.

Use **Add Wheat, Carrots and Potatoes** for initial crop presets. In **Crops**, choose existing stackable seed and harvest items, cycle seconds, yield, XP and required farming level. The initial presets take 30, 45 and 60 minutes and yield 10, 15 and 20 items. Assign seed/output items before enabling. Each crop keeps a stable identifier; do not delete/recreate it to rename it. Optional tileset graphics use three horizontal frames, each one map tile wide, starting at `SourceX`/`SourceY` in pixels. Workers use existing character sprite animation frames. These workers have no combat, loot, navigation or collision AI; their server-side production is authoritative and their visible work animation is rendered on the exterior.

Defaults: two workers, 500 stored harvest items, 100 currency to hire a novice and 5 currency per started cycle. Experts cost twice as much to hire and pay, finish cycles 20% sooner and add 25% yield rounded down, with a minimum bonus of one item. Maximum configurable workers: eight. Enable the farm, close its settings window and **Save** the housing configuration. Settings are stored in server `resources/housing.json`; authored crop names enter the existing editable content translation catalog. Built-in names and controls include English, French and Spanish.

## Player flow

Open **My house > Farm**, hire a worker and select a crop for that worker. Deposit seeds from inventory in batches of ten and salary funds in batches of 100. The salary fund is capped at 1,000,000 and seed storage at 10,000 total items. Unspent salaries can be withdrawn. No money is taken automatically from the player's inventory after depositing it.

Each worker is permanently assigned to one plot until dismissed. Selecting a crop enables continuous production. One seed, the full cycle wage and enough future granary space are reserved before work starts. A worker pauses automatically if any requirement fails. Depositing supplies or collecting stored produce resumes eligible plots immediately. Failed operations leave inventory and the farm unchanged.

**Pause after harvest** completes a prepaid cycle and stops further planting. A worker can be dismissed only after their current cycle finishes. Crop changes also wait for the current harvest, preserving the paid cycle. Harvests are collected per output item, with a capacity check before the transfer; full inventory never sends overflow to the ground or bank. Cycles already underway retain their saved output, yield and XP if crop definitions change. Salary balances retain their original currency and must be withdrawn before funding a different currency.

The native **Farmer** profession is learned with the first hire and uses the existing profession profile/condition queries. Completed cycles grant persistent farming XP, with 100 levels and a base requirement of 100 XP growing by 8% each level. Its reserved ID is `f0c40000-0000-4000-8000-000000000001`. This initial native progression has no configured resource links or level-reward events; its definition is restored by the server when profession settings load. Configure crop unlock levels in the farm editor.

## Offline production and persistence

Saved UTC start/due times and resource reservations drive an event simulation. Online and offline production use the same calculation. The server settles due work on the player's update (at most once every 30 seconds), on opening the house window and before every farm operation. Only production transitions require background database checkpoints. No continuously loaded NPC or map is required while the player is absent.

An absence is settled at the next interaction/login, including across server restarts. It can finish multiple cycles while seeds, salary funds and storage permit. Blocked time is discarded: replenishing a fund or emptying storage cannot generate immediate backdated produce. Repeating an action at the same time or moving the clock backward does not grant a second harvest. Farm/inventory changes are checkpointed together using the existing player save path and restored if saving fails. Configuration changes govern new cycles; already started cycles preserve their saved rewards. Disabling farming prevents new production while disabled; a prepaid cycle may finish when re-enabled. There is no automatic sale, spoilage, water/tool consumption or employee departure in this first version.

## Verification

Run `dotnet run --project Utilities/FarmingTests/FarmingTests.csproj` from the repository with the normal .NET 8 build prerequisites. The executable checks online/offline equivalence, restart persistence, wage/seed conservation, shared storage reservations, pauses, resource exhaustion, level gates, unchanged in-flight rewards, currency changes, owner checks, forged/negative requests, failed-save rollback and locale coverage/placeholder compatibility/English fallback.

After configuring content, validate in game with two accounts: hire both worker types, deposit supplies, select crops, visit the exterior, observe growth and work animations, reconnect after multiple cycles, restart the server, fill the granary and inventory, and confirm that a visitor cannot manage or read another owner's farm finances. Native UI rendering and multiplayer behavior require this content-backed check before deployment. This change does not deploy to a running server.
