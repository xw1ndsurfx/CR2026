# Royal Archery: persistent rankings

This delivery adds a persistent best-score leaderboard to the existing local-range
competition. It does not award currency, items, XP, titles or achievements.

## Author and use

In Archery Mini-Game Editor, select the existing range and enable RankedCompetition.
CompetitionEnabled still controls whether players can register. RankedCompetition defaults
to false for existing configurations. SeasonKey defaults to `royal-1`; use a new stable key
for each new season (1–40 ASCII letters/digits, hyphens or underscores). Save the configuration.

The first registration snapshots ranked status, season, rules and wind for that competition.
Editing those fields during a registration or match does not reclassify its results.
Completed server-owned cards are saved as soon as the match finishes. Solo training,
unranked competitions, incomplete cards and forfeits never enter persistent rankings.

Players open the existing archery window and choose Today, This week or Season. Next range
cycles enabled ranked ranges, prioritizing the current map. Standings returns to the live
competition board; another click returns to shooting. Ranking controls are disabled during
draw/flight. Starting a match restores the shooting viewport. Queries are explicit and
responses are correlated so a delayed response cannot replace a newer period selection.

* Today: 00:00 UTC to the next UTC midnight.
* This week: Monday 00:00 UTC to the following Monday.
* Season: all committed cards with the configured season key, up to the current time.

All periods also filter by range and the exact scoring rules. Each character occupies one
row with their best score and completed-card count in that period. Scores are not summed;
playing more cards does not break an equal best score. Equal best scores share a place.
Display names are recorded at completion; the latest recorded name in the period is shown.
Names remain player-provided text and are not translated. Range names use the content catalog.

## Rule changes and storage

`resources/weapon-masteries.db` receives the additive ArcheryResults table and lookup index.
There are no account/item table edits. Existing mastery/ammo/talent rows are preserved.
Each row has a competition/character primary key. Repeated delivery/retry cannot count a
second card or replace a completed score. SQLite commits the insert before it is advertised
as saved. Back up this database and royal-combat.json while the server is stopped.

A SHA-256 rules fingerprint covers the physics/scoring version, wind, rounds, arrows,
timers, distances and target geometry/motion. Changing a scored setting starts a separate
ladder automatically. Old rows remain in the database; the current UI/API shows the current
season and rules only. Reusing a previous season key restores its compatible results.
When physics/scoring changes, increment physicsVersion in ArcheryRulesKey.

Failed writes remain in an in-memory retry queue. Retry batches are bounded to 16 rows and
back off five seconds after a failure; retries run during competition updates, exits or
subsequent ranking reads. Database I/O is outside the registration lock. The final board
indicates when ranked results await saving, and failures are logged. With no competition
updates or ranking reads, pending retries wait for the next activity. **An uncommitted retry
queue does not survive process termination.** Committed cards survive disconnect/restart;
registrations and live competition state are still in memory. No reward/entry-fee recovery
is involved. Treat disk/database errors as an operational issue before restarting.

## Native leaderboard/wiki integration

LeaderboardDataRuntime.Archery is the common read path for the game and existing Wiki API:

`GET /api/v1/wiki/leaderboard/archery?rangeId=<existing-guid>&period=day&limit=50`

`period` accepts day/week/season; limits are clamped to 1–100 (the game shows 16). The API
returns range/season/period/maximum plus rank, display name, best score and completed cards.
Character/account IDs are not exposed. Unknown or disabled/unranked ranges return 404,
invalid periods 400 and storage failures 503. This follows the existing anonymous Wiki
leaderboard surface; no score-writing endpoint is added.

Reads use a bounded five-second snapshot cache. A new committed result, a UTC date change
or a configuration/rules/season change invalidates the relevant view. Player refresh requests
are rate-limited; clicks queued on the client request the latest selected period.

## Verification and remaining work

Tests cover repeated/concurrent delivery, persisted reload, old mastery-database preservation,
best-per-character/shared places, UTC midnight/Monday and future exclusion, range/season/rules
isolation, failed-write retry, an actual finished server player card, API/name-only response
and cache invalidation. Native client controls test sixteen rows, stale responses and live
updates while historical rankings are open. These are not production-world/network load tests.

Still required: 16-client play/load tests, reconnect, archived-season browsing, retention/admin
tools, finals/brackets, rewards, guild/spectator modes, full cinematics and production artwork.
Mastery respec/public inspection and the remaining weapon/naval content are also unfinished.
