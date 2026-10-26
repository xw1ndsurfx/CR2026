# Royal Archery: first multiplayer competition

This delivery adds a server-owned registration pool and live/final standings to the existing
archery range. It is a simultaneous score competition with separate shooting cards, not an
elimination bracket or persistent ranked season. No rewards or mastery XP are awarded.

## Configure and play

1. Deploy matching client/server/editor builds on a development copy. Back up resources.
2. In Archery Mini-Game Editor, select an existing enabled range. Set CompetitionEnabled
   to true, MinimumPlayers (2–16), MaximumPlayers (minimum–16) and RegistrationSeconds
   (10–120, default 30). Save. Range rules and targets still configures the shooting card.
3. Participants stand within two tiles horizontally/vertically of that range's shooting
   position, on the same map and instance, outside combat and incompatible activities.
   Unlike solo training, they need not occupy the same tile. Each participant is held at
   their own entry tile throughout registration and shooting.
4. Open the existing archery window through the NPC or Character action, then Join competition.
   Registration shows entrants, minimum count and remaining time. Leaving cancels that entry.
5. The competition starts when full, or at the deadline if its minimum is met. Otherwise it
   cancels and releases movement. Late entrants form a new registration pool.
6. Shoot normally. Standings toggles the 16-entry board; it is disabled during draw/flight.
   When everyone finishes or withdraws, final standings appear. Tied scores share a place;
   withdrawals have no place. Closing the window leaves the competition.

CompetitionEnabled defaults to false, preserving existing authored ranges. There is no new
map, NPC ID or inventory asset. Players remain visible on the native 2D map; a spectator
scoreboard, separate tournament instance and ceremony are not implemented.

## Authority, fairness and lifecycle

The first registration snapshots the range's rules and wind. All entrants receive this same
card and common server start time; subsequent configuration edits do not change that group.
Each player's server-owned ArcheryMatch computes charge, sway, moving-target interception,
round deadlines and score. Players shoot at their own pace; rounds are not synchronized
between participants. Mastery, equipment stats and inventory ammunition do not alter the card.

The protocol adds only JoinCompetition and server-to-client standings. Clients cannot send
their score, choose an opponent's result or submit a hit position. Score aggregation receives
only the actual player's server match state, is monotonic and freezes completed results.
No cross-player callbacks occur under the registration lock, avoiding nested player locks.
Aggregation and board updates are limited to four per second per participant; local match
advancement keeps its existing server-update cadence.

A character cannot register twice while active. Pools are isolated by range and map instance,
and protected against concurrent overbooking. Leaving before start removes registration;
leaving, moving, changing map/instance, dying, entering combat or disconnecting during play
forfeits that participant. Existing movement/combat/lobby holds also cover registration.
Opening then immediately closing before the first snapshot is handled with a per-start token;
a stale/wrong token cannot cancel a newer session.

Server player updates maintain a ten-second participation lease. If updates stop, queued
entrants expire or active entrants forfeit. A server pause exceeding that lease may therefore
cancel a competition; production load testing and lease tuning remain required. Completed
boards are retained while participants inspect them, then cleaned after two minutes without
polling, with a one-hour hard retention limit. Restart loses in-memory registrations/results;
no entry fees or inventory changes require recovery. Persistent rankings/reconnect are future work.

## Verification and limits

Automated tests cover concurrent 16-character registration, duplicates/capacity, instance
isolation, snapshots/common start, ties, immutable completed scores, withdrawals, expiry,
insufficient-player cancellation and actual two-player server-handler/hold integration.
Native Gwen tests cover all 16 board rows, registration/start/final transitions and late
updates after close, alongside existing viewport tests. These tests do not constitute a
16-client production load test or a production-world visual run.

Still required: real multi-client network playtesting, sustained load/disconnect testing,
persistent day/week/season boards, rewards, matchmaking beyond local range registration,
finals/brackets, guild competition, spectator UI, reconnect, cinematics and production art.
