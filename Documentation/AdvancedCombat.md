# Corps Royaux — Advanced PvE combat (first playable iteration)

This implementation is **opt-in**, disabled by default until the server and client are deployed together. Set `Combat.EnableAdvancedCombat = true` in server configuration after testing and restart the server. Default binds preserve the existing **E attack** and **Q/right-click shield block**; added **Left Shift dodge** and **R counterattack** can be remapped in Controls. Player-facing control names are localized in en/fr/es.

- Dodge: one collision-aware tile using the existing dash packet/animation, server-enforced 3 s cooldown, 180 ms protection against NPC **weapon** strikes. No protection against PvP or spells in this iteration.
- Perfect parry: block with an equipped shield within 200 ms of the server receiving the blocking command. Negates one NPC weapon hit and opens a 1.5 s counter window against that NPC.
- Counter: server validates alive/position/range/target and processes a normal weapon attack with +25% damage against that NPC only. The bonus is single-use; no direct client-reported damage.
- Boss telegraph: boss NPCs warn their current target for 650 ms, with a red target tile. Moving off the tile avoids the hit. Boss spell attacks, cones, circles, stamina, and editor-authored patterns are **not implemented** yet.

Server options: `EnableAdvancedCombat`, `AdvancedDodgeCooldownMs`, `AdvancedDodgeInvulnerabilityMs`, `AdvancedParryWindowMs`, `AdvancedCounterWindowMs`, `AdvancedCounterBonusPercent`, `BossTelegraphDurationMs`. Times and percent values are clamped on use.

## Acceptance checklist

1. Upgrade both client/server; enable feature on test server, confirm legacy Q/E attacks and shield still work.
2. Boss attack shows a red telegraph and resolves only if the player remains on that tile.
3. Dodge into a wall should fail and not move; successful dodge should broadcast the existing dash movement.
4. Perfect shield parry prevents PvE weapon damage and allows exactly one retaliatory bonus hit.
5. Spam R or dodge packets cannot bypass cooldowns, PvP rules, map instances, collision, or server damage validation.
6. Test on Windows/Linux builds and under real client latency before promoting to production.

**Known limitations:** No build was executed in the authoring environment. This is an early PvE integration to review and test before merge. **No** stamina bar, advanced enemy pattern editor, custom SFX, or additional graphical sprite assets yet.
