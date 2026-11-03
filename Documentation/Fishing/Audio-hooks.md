# Pêche — sons effectivement intégrés

Cette matrice décrit les appels présents dans le code. Le manifeste du pack est un inventaire de ressources ; il ne prouve pas, à lui seul, l’intégration d’un événement.

## Réglages qui agissent en jeu

L’onglet **Sons** configure `File`, `Volume` (0–100), `Loop`, `CooldownMs`, `MaximumInstances`, `Spatial` et `Range`. Les overrides suivent **prise → outil → zone → global → défaut**. L’absence d’entrée hérite ; un fichier vide impose le silence. À l’intérieur d’un même niveau, la clé précise est prioritaire, puis son cue commun et les alias de compatibilité. Il est préférable de définir une seule clé précise par action, plutôt que plusieurs alias concurrents.

`Spatial` passe par le mixeur de carte, à la position du joueur local. Il ne diffuse pas automatiquement les sons aux autres joueurs. La portée minimale utilisée par le mixeur est une tuile. Les animations d’équipement, elles, sont répliquées aux observateurs proches.

Le pack contient également des métadonnées `bus`, `pitch`, `fade_duration` et `gain_override`. Ces champs ne sont pas des réglages du modèle runtime et ne sont pas présentés comme des contrôles actifs. Le fondu des boucles à une transition est fixé à 160 ms ; fermeture et déconnexion les arrêtent immédiatement. La préécoute de l’éditeur utilise le volume système et lit les WAV. Les OGG peuvent être assignés et doivent être contrôlés dans le client.

Les erreurs avant l’ouverture transmettent le cue résolu dans `FishingStatePacket.NoticeSoundJson`. Elles jouent une seule fois, avec limites de volume, simultanéité et cooldown ; la boucle est forcée à `false` pour ne pas laisser une activité sonore sans fenêtre propriétaire. Le cue de fermeture est également non bouclé. Les entrées `reeling_stop` et `line_tension_exit` arrêtent toujours leurs boucles même si leur cue additionnel est muet.

## Propriété et durée des sons

`FishingAudio` possède les boucles et les cues de sa session. Les états serveur sont dédupliqués par `SessionId`, `Revision` et `SoundSequence` ; un rafraîchissement d’état ne relance pas une boucle. Les cues de gain XP attendent 650 ms après le résultat, et un passage de niveau remplace le cue XP simple. Le son de lancer contient déjà le contact avec l’eau : `splash` est un hook fonctionnel, déclenché 220 ms après le lancer, mais il reste silencieux tant qu’un override explicite n’est pas défini.

Une rencontre légendaire suspend le mode AUTO pendant son invitation. Si le joueur l’ignore jusqu’au délai, la créature est perdue sans récompense et AUTO reprend après 1 500 ms. Dès que le joueur accepte, le combat et le défi restent manuels ; ils ne réactivent pas AUTO implicitement. Le défi implémenté demande une série de directions dans un temps limité. Les deux anciens identifiants de défis autonomes `challenge_timing_prompt` et `challenge_reel_prompt` restent prévisualisables pour inventorier le pack, avec l’indication **Réservé** et leurs contrôles d’override désactivés. Ils ne sont jamais choisis comme alias à la place d’un défi de direction.

## Matrice des 68 clés

**Direct** : cue émis sur le chemin indiqué. **Alias / famille / fallback** : réglage utilisé par la résolution d’une action existante, sans deuxième lecture automatique. **Optionnel** : hook actif mais silencieux par défaut. **Réservé** : aucun gameplay correspondant dans cette version.

| Clé | Statut | Appel ou famille réelle | Ressource par défaut | Comportement |
|---|---|---|---|---|
| `ui_open` | alias | `session_start` | `fishing/fishing_ui_open.wav` | Ouverture validée par le serveur ; secours client si aucun cue serveur. |
| `ui_close` | alias | `session_end` | `fishing/fishing_ui_close.wav` | Fermeture normale : une lecture non bouclée après nettoyage. |
| `ui_select` | direct | `FishingWindow.AddButton / ChangePage` | `fishing/fishing_ui_select.wav` | Survol des boutons et pagination des piles. |
| `ui_confirm` | direct | `FishingWindow.Send / SetAutomatic` | `fishing/fishing_ui_confirm.wav` | Lancer, accepter le défi, programmer le mode automatique. |
| `ui_error` | direct | `Player.FishingError / FishingWindow.Apply` | `fishing/fishing_ui_error.wav` | Refus initial, erreur de lecture ou retour utilisateur sans cue précis. |
| `cast_rod` | alias | `cast (Rod)` | `fishing/fishing_cast_rod.wav` | Famille choisie selon l’outil équipé. |
| `cast_net` | alias | `cast (Net)` | `fishing/fishing_cast_net.wav` | Famille choisie selon l’outil équipé. |
| `cast_cage` | alias | `cast (Cage)` | `fishing/fishing_cast_cage.wav` | Famille choisie selon l’outil équipé. |
| `splash` | optional | `FishingAudio.Apply → Waiting +220 ms` | `` | Silencieux par défaut : les fichiers de lancer incluent déjà le contact avec l’eau. Un override le déclenche. |
| `bite` | direct | `Player.StartFishingFight` | `fishing/fishing_bite.wav` | Une prise a mordu ; début du combat. |
| `reel_loop` | alias | `reel` | `fishing/fishing_reel_loop.wav` | Combat + moulinet maintenu, ou résolution automatique. |
| `tension_loop` | alias | `tension` | `fishing/fishing_tension_loop.wav` | Combat et tension au moins 75 %. |
| `line_break` | direct | `Player.UpdateFishing` | `fishing/fishing_line_break.wav` | État de ligne épuisé / rupture validée par le serveur. |
| `escape` | direct | `Player.FailFishing` | `fishing/fishing_escape.wav` | Fuite ordinaire ou délai de combat écoulé. |
| `catch` | alias | `reward_item` | `fishing/fishing_catch.wav` | Prise ordinaire ajoutée à l’inventaire. |
| `crate_raise` | alias | `cage_retrieve` | `fishing/fishing_crate_raise.wav` | Remontée visuelle d’une prise obtenue à la cage. |
| `net_retrieve` | direct | `FishingAudio.Apply → Caught` | `fishing/fishing_net_retrieve.wav` | Prise obtenue avec le filet. |
| `skill_stabilize` | direct | `Player.UseFishingSkill(0)` | `fishing/fishing_skill_stabilize.wav` | Compétence acceptée par le serveur. |
| `skill_pull` | direct | `Player.UseFishingSkill(1)` | `fishing/fishing_skill_pull.wav` | Compétence acceptée par le serveur. |
| `skill_repair` | direct | `Player.UseFishingSkill(2)` | `fishing/fishing_skill_repair.wav` | Compétence acceptée par le serveur. |
| `skill_sink` | direct | `Player.UseFishingSkill(3)` | `fishing/fishing_skill_sink.wav` | Compétence acceptée par le serveur. |
| `auto_start` | direct | `Player.SetMode / FishingAudio.Apply` | `fishing/fishing_auto_start.wav` | Passage effectif au mode automatique ; transition client conserve le cue si Cast a suivi dans le même tick. |
| `auto_stop` | direct | `Player.SetMode / FishingAudio.Apply` | `fishing/fishing_auto_stop.wav` | Retour effectif au mode manuel entre les prises. |
| `auto_pause` | fallback | `auto_bait_empty / auto_inventory_full / auto_legendary_wait` | `fishing/fishing_auto_pause.wav` | Valeur commune héritée par les trois pauses ; les cues précis ont priorité. |
| `legendary_appear` | alias | `legendary_hooked` | `fishing/fishing_legendary_appear.wav` | Une rencontre légendaire vient d’être tirée. |
| `legendary_challenge_cue` | alias | `challenge` | `fishing/fishing_legendary_challenge_cue.wav` | Nouvelle direction à suivre dans le défi. |
| `legendary_challenge_success` | alias | `challenge_success` | `fishing/fishing_legendary_challenge_success.wav` | Dernière étape réussie ; reprise du combat. |
| `legendary_challenge_fail` | alias | `legendary_timeout` | `fishing/fishing_legendary_challenge_fail.wav` | Défi expiré. Une mauvaise direction utilise legendary_escape. |
| `legendary_catch` | direct | `Player.CompleteFishing` | `fishing/fishing_legendary_catch.wav` | Créature légendaire effectivement récompensée. |
| `xp_gain` | alias | `reward_xp` | `fishing/fishing_xp_gain.wav` | Gain de progression confirmé, différé de 650 ms après réception. |
| `level_up` | direct | `FishingAudio.Apply` | `fishing/fishing_level_up.wav` | Niveau augmenté ; remplace le cue XP simple pour ce gain. |
| `ambience_sea_loop` | alias | `ambience` | `fishing/fishing_ambience_sea_loop.wav` | Ambiance de la scène pendant une activité, sauf Ready / prompt légendaire / fermeture. |
| `session_start` | direct | `Player.OpenFishingFromEvent` | `fishing/fishing_ui_open.wav` | Événement valide, zone et outil acceptés. |
| `session_end` | direct | `FishingWindow.Destroy / Player.EndFishing` | `fishing/fishing_ui_close.wav` | Fermeture normale. Toutes les boucles sont arrêtées avant le cue final. |
| `session_interrupted` | direct | `Player.EndFishing` | `fishing/fishing_auto_stop.wav` | Interruption de session. Lecture finale forcée sans boucle. |
| `tool_changed` | direct | `Player.ValidateFishingEquipment` | `fishing/fishing_ui_select.wav` | Changement d’outil équipé entraînant la fermeture. |
| `tool_missing` | direct | `Player.FishingError / NoticeSoundJson` | `fishing/fishing_ui_error.wav` | Aucun outil de pêche compatible équipé avant l’ouverture. |
| `tool_incompatible` | direct | `Player.CastFishing / CompleteFishing` | `fishing/fishing_ui_error.wav` | Aucune prise éligible avec cet outil et les conditions actuelles. |
| `bait_missing` | direct | `Player.CastFishing` | `fishing/fishing_ui_error.wav` | Appâts manquants en mode manuel. |
| `inventory_full` | direct | `Player.CastFishing / CompleteFishing` | `fishing/fishing_ui_error.wav` | Récompense impossible faute de place en mode manuel. |
| `zone_unavailable` | direct | `Player.FishingError / NoticeSoundJson` | `fishing/fishing_ui_error.wav` | Zone absente, désactivée ou inaccessible depuis la position du joueur. |
| `legendary_hooked` | direct | `Player.FishingBite` | `fishing/fishing_legendary_appear.wav` | Ouverture de l’invitation légendaire. |
| `challenge_timing_prompt` | reserved | `—` | `fishing/fishing_legendary_challenge_cue.wav` | Réservé : aucun défi de timing autonome dans cette version. Prévisualisation seulement, override désactivé dans l’éditeur. |
| `challenge_direction_prompt` | alias | `challenge` | `fishing/fishing_legendary_challenge_cue.wav` | Le défi implémenté est une suite de directions avec une fenêtre de réponse. |
| `challenge_reel_prompt` | reserved | `—` | `fishing/fishing_legendary_challenge_cue.wav` | Réservé : aucun défi autonome de moulinet dans cette version. Prévisualisation seulement, override désactivé dans l’éditeur. |
| `legendary_timeout` | direct | `Player.UpdateFishing` | `fishing/fishing_legendary_challenge_fail.wav` | Invitation ou fenêtre de défi expirée. Une invitation ignorée provenant du mode AUTO reprend AUTO après 1 500 ms ; une prise acceptée reste manuelle. |
| `legendary_escape` | direct | `Player.ChallengeInput / UpdateFishing` | `fishing/fishing_escape.wav` | Mauvaise direction ou temps de combat légendaire écoulé. |
| `auto_bait_empty` | direct | `Player.CastFishing` | `fishing/fishing_auto_pause.wav` | Manque d’appâts pendant la pêche automatique ; arrêt effectif. |
| `auto_inventory_full` | direct | `Player.CastFishing / CompleteFishing` | `fishing/fishing_auto_pause.wav` | Manque de place pendant la pêche automatique ; arrêt effectif. |
| `auto_legendary_wait` | direct | `FishingAudio.Apply` | `fishing/fishing_auto_pause.wav` | Transition automatique vers l’invitation légendaire ; le cue complète la rencontre sans remplacer son cue principal. |
| `reward_item` | direct | `Player.CompleteFishing` | `fishing/fishing_catch.wav` | Récompense ordinaire finalisée, une seule fois par séquence. |
| `reward_xp` | direct | `FishingAudio.Apply` | `fishing/fishing_xp_gain.wav` | Progression accrue sans passage de niveau ; cue retardé de 650 ms. |
| `rod_cast` | alias | `cast (Rod)` | `fishing/fishing_cast_rod.wav` | Alias du lancer à la canne. |
| `net_cast` | alias | `cast (Net)` | `fishing/fishing_cast_net.wav` | Alias du lancer au filet. |
| `cage_cast` | alias | `cast (Cage)` | `fishing/fishing_cast_cage.wav` | Alias de la mise à l’eau de la cage. |
| `cage_retrieve` | direct | `FishingAudio.Apply → Caught` | `fishing/fishing_crate_raise.wav` | Prise obtenue avec une cage. |
| `line_tension_enter` | alias | `tension` | `fishing/fishing_tension_loop.wav` | Entrée dans la zone de tension élevée (75 %). |
| `reeling_start` | alias | `reel` | `fishing/fishing_reel_loop.wav` | Entrée dans l’état de moulinet actif. |
| `reeling_stop` | optional | `FishingAudio.Apply` | `` | Sortie de l’état moulinet : coupe toujours la boucle, cue additionnel silencieux par défaut. |
| `line_tension_exit` | optional | `FishingAudio.Apply` | `` | Sortie de la zone de tension : coupe toujours la boucle, cue additionnel silencieux par défaut. |
| `cast` | family | `Player.CastFishing` | `fishing/fishing_cast_rod.wav` | Résout Rod / Cage / Net selon le type équipé. |
| `reel` | family | `FishingAudio.Apply / Player.SetReeling` | `fishing/fishing_reel_loop.wav` | Famille de moulinet ; propriétaire d’une boucle au maximum. |
| `tension` | family | `FishingAudio.Apply` | `fishing/fishing_tension_loop.wav` | Famille de tension ; seuil 75 %. |
| `ambience` | family | `FishingAudio.Apply` | `fishing/fishing_ambience_sea_loop.wav` | Ambiance de scène, remise à zéro à la fermeture / déconnexion. |
| `legendary` | family | `legendary_hooked` | `fishing/fishing_legendary_appear.wav` | Famille de compatibilité pour l’apparition légendaire. |
| `challenge` | family | `Player.BeginFishingChallenge / ChallengeInput` | `fishing/fishing_legendary_challenge_cue.wav` | Étape du défi de directions. |
| `challenge_success` | family | `Player.ChallengeInput` | `fishing/fishing_legendary_challenge_success.wav` | Étapes requises validées. |
| `challenge_fail` | family | `legendary_timeout` | `fishing/fishing_legendary_challenge_fail.wav` | Famille de compatibilité pour la réponse expirée. |

## Sources de l’implémentation

- `Framework/Intersect.Framework.Core/Fishing/FishingSoundCatalog.cs` : bindings, valeurs par défaut et héritage.
- `Intersect.Server.Core/Entities/Player.Fishing.cs` : événements validés côté serveur, erreurs et transitions.
- `Intersect.Client.Core/Interface/Game/FishingAudio.cs` : boucles, déduplication, cues de présentation et nettoyage.
- `Intersect.Client.Core/Interface/Game/FishingWindow.cs` : survols, confirmations et fermeture.
- `Intersect.Editor/Forms/Editors/FrmFishing.Sounds.cs` : édition et préécoute réelles.

Une compilation ne remplace pas l’écoute sur le périphérique cible. La matrice ne prétend pas constituer une validation auditive Windows ou un essai multijoueur de production.
