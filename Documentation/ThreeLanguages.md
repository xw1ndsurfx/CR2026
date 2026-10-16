# English, French and Spanish

Players choose **Settings > Game > Interface > Language**. Apply the setting, then restart the client. The preference is local to each player; the default and fallback are English for interface text. Authored content falls back to its original text.

The built-in UI catalog is `Intersect.Client.Core/Localization/Locales/ui.json`, embedded in the client. Each entry contains `en`, `fr`, and `es`. Keep placeholders and event substitution commands unchanged. The repository's `AGENTS.md` makes all three translations a requirement for future player-facing text.

## Authoring and automatic translation

Open **Game Editors > Game Translations** in GameEditor while connected as an editor.

1. Enter a LibreTranslate-compatible `/translate` URL and its API key if required. HTTPS is recommended for a remote service. A self-hosted service can also be configured.
2. Select the original language (`auto` handles content written in mixed languages), enable automatic translation and save the settings.
3. Select **Translate existing content** to queue existing dialogue, choices, descriptions, quests, item/NPC names and built-in server messages. Use **Refresh** to see progress.
4. Edit the English/French/Spanish cells and select **Save corrections** when wording or proper names need adjustment.

After configuration, saving content in the editor queues missing translations in the background. Saving the game object never waits for the service. Identical source phrases reuse cached translations. Changing the source phrase produces a separate entry. API failure preserves the source and does not publish an incomplete translation. The dialog reports service status; reconnect/reopen an existing window if its old labels remain cached.

Settings and credentials are stored on the server in `config/translation-service.private.json`. Do not put this file in a client/update package or commit it. Cached, editable text is stored in `resources/languages/content.json`. Only that public catalog is sent to players, not the private configuration. A matching client/server/GameEditor build is required.

Event variables are protected before API calls; display dialogue is translated before player/time values are substituted. Filenames, IDs and expressions are excluded. Player chat, player names and guild names are not sent for translation. Automatic translation is a draft: review story text, names, terminology and agreement in context.

The endpoint/key are intentionally unconfigured by default. No content is sent until an authenticated editor enables and saves the service. Hosted services may require their own subscription or API key.

Provider protocol: https://docs.libretranslate.com/api/operations/translate/
