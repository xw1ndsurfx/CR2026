# Blackjack mini-game

## Roadmap

Blackjack is the second Intersect mini-game and is being built on the shared mini-game infrastructure created for Poker.

### Part 1 - foundation

- `MiniGameType.Blackjack` is reserved.
- Stable progression key: `blackjack`.
- Blackjack XP, wins and level are persisted independently from Poker by the existing `CharacterId + GameKey` storage key.
- The level curve is currently the shared mini-game curve: levels 1 through 25.
- A Blackjack progression service wraps the generic persistence store for the future runtime.
- Blackjack is registered in the mini-game catalog but intentionally not exposed as playable until the gameplay runtime exists in Part 2.
- The server fails closed if an unfinished Blackjack command is supplied; it never routes it into Poker.

No cards, bets or payouts are implemented in Part 1.
