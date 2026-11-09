# Weapon skills HUD

Active, unlocked techniques for the equipped weapon appear in a compact gold-edged panel above the hotbar. The panel sizes itself to one to three columns, shows the weapon icon, and includes disabled-state styling and cooldown progress. Visible bitmap glyph metrics keep the text readable with the global Empire 7 size-10 setting. No keyboard shortcuts or mastery costs change.

The bar is available only with a local player on land, outside visible mini-games and blocking overlays. Archery, fishing, Exportation, cooking, poker, blackjack, potions, roulette, dice, lockpicking, metalworking, jousting, duels and spectator mini-game views suppress it. Boarding a ship hides it; disembarking restores it if the equipped weapon has learned eligible techniques. Late cooldown/mastery packets cannot make it reappear while suppressed. Hidden controls also reject activation callbacks. Cooldown state remains synchronized while hidden.
