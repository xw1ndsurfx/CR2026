# Player-facing languages

The game supports English (`en`), French (`fr`) and Spanish (`es`).

- Every new or changed player-facing text must have all three translations in the same change. Use the localization catalog; do not introduce hardcoded player-facing labels.
- Preserve formatting placeholders, event substitution commands, markup and identifiers exactly. Do not translate asset filenames, protocol values, player names, guild names or player chat.
- Authored game content (dialogues, choices, quests, descriptions and NPC/item names) uses the content translation catalog. Automatic translations must be cached, remain editable and fall back to the source when unavailable.
- Translation service credentials belong in the editor/server configuration, never the distributed client. An external translation service must be explicitly configured before sending content to it.
- Validate locale coverage and placeholder compatibility, plus preference persistence and English fallback, when changing localization.
