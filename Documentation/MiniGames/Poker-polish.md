# Poker polish configuration

- Unlimited NPC bankroll is an explicit per-table option. When enabled the house mints only the shortfall required to seat an NPC, so the dealer/opponents do not disappear because the reserve reached zero. Human inventory balances are never topped up.
- Player all-ins are not rigged. The deck remains server-random. NPC policy instead responds more intelligently to an opponent all-in, reducing the easy repeated-shove exploit without changing winning cards.
- The Sounds / animations dialog exposes separate Check, Call, Raise/Bet, Fold, All-in and Level-up animations plus optional WAV files for Deal, Check, Call, Raise, Fold, All-in, Win and Level-up. Existing Deal/Victory animations remain on the main dialog.
- Level-up reward common event points at an existing common event, allowing normal event commands to define rewards.

Sound files come from resources/sounds. If a selected animation contains a sound, that sound takes priority. The explicit WAV cue is used as a fallback, preventing accidental double playback.
