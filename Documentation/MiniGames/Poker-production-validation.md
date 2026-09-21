# Poker production validation

This checklist is the acceptance gate for the Poker feature before deployment.

## Automated gate

The `Poker production validation` workflow must pass on the exact pull-request head being merged.

It runs these standalone regression suites:

- Poker core, registry, NPC automation and fairness boundaries.
- Serialized network protocol and two-client presentation.
- Persistent Poker progression and replay/idempotency checks.
- Funded-money ledger, restart and escrow rules.
- Real inventory transaction checks.
- Client Poker UI regressions.
- Event Editor mini-game configuration tests.

It also builds the real Server.Core, Server, Client and Editor projects from the same commit.

The older focused Poker workflows remain useful for quicker diagnostics; the production workflow is the consolidated release gate.

## Invariants that must remain true

- The server is authoritative for cards, betting, pots, payouts and progression.
- NPC decisions never read opponents' hidden cards.
- A positive Poker win means positive net gain, not gross pot.
- Test chips never enter player inventory.
- Funded play removes the configured inventory currency through escrow and returns settled balances safely.
- Level XP/rewards and quest progress are not duplicated by refresh, sweep or replay.
- NPC action/win chat stays local to the table.
- Human global win announcements occur only when the event option is enabled.
- Unsupported mini-game types fail closed.
- Client/server/editor binaries must come from the same compatible build.

## Manual release smoke test

Before deploying a new production build, use disposable test characters/items and verify:

1. Join a test-chip table, play a hand, leave and rejoin.
2. Join a funded table, verify buy-in debit, settlement and refund.
3. Trigger a positive-net win and verify Poker XP, quest progress and configured level rewards.
4. Verify B1-B6 selection/unlock behavior.
5. Verify NPC local chat and optional human GLOBAL win announcement.
6. Disconnect/reconnect during a hand and confirm the table settles without duplicated money or XP.
7. Open the Poker event editor, save/cancel a configuration, and reopen it.
8. Launch matching Client + Server binaries built from the same commit.

A green CI run is not a substitute for dependency/security review or backups of production player data.
