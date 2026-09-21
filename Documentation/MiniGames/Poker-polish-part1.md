# Poker polish - Part 1

This branch intentionally covers only gameplay presentation, NPC availability and all-in AI.
Quest objectives, level-up rewards and level-up-specific effects are reserved for Part 2.

## Action audio and animation

The Start Mini-Game -> Poker editor can select an animation and/or WAV sound for:
deal, check, call, raise, fold, all-in, showdown, the local player's turn, victory,
defeat/no-profit, and leave.

Animations remain local screen overlays. If the selected Intersect Animation has its own
Sound configured, PokerScreenEffect also plays that embedded sound. A direct action sound
can be selected independently from resources/sounds. If both are configured, both play.

Reopening an existing table requires identical settings on all access events. Missing sound
or animation files remain explicit selections instead of silently changing to another asset.

## Unlimited NPC bankroll

Funded tables may opt into Unlimited NPC bankroll. This deliberately creates NPC stakes as
needed and burns their remaining stake when the NPC leaves. Human inventory is never
charged for an NPC buy-in. Human winnings against these NPCs therefore create new units of
the selected currency, making this option an intentional economy faucet.

Finite mode is unchanged and keeps the exact legacy house identity/reserve. Unlimited mode
uses a separate house identity, so enabling it does not consume or overwrite the previous
finite reserve.

## All-in fairness

Cards and the shuffle are unchanged. All-in outcomes are never rigged. Instead, NPC policy
now recognizes large pressure: strong hands call reliably, medium hands defend based on
strength/pot odds plus controlled variance, and weak hands may still fold. This makes easy
all-in bluffs less successful without changing poker probabilities.

## Part 2

Part 2 will add independent Poker progression hooks:
- configurable level-up animation/sound,
- rewards tied to reaching Poker levels,
- quest objectives for cumulative Poker winnings, hands/games won, and Poker level,
- server-side quest progress updates driven by committed funded/test Poker results.
