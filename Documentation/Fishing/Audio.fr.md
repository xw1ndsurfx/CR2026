# Pêche — Contrat audio

**Statut : pack audio disponible ; raccordement au jeu en cours, à confirmer dans le rapport final.** Ce document distingue les ressources sonores, leur configuration et les événements réellement joués.

## Ressources et provenance

Le pack contient **32 WAV** et **58 bindings d’événements**. Les bindings supplémentaires réutilisent des effets existants ; ils ne représentent pas 58 enregistrements distincts. Les fichiers sont issus d’une synthèse procédurale originale pour ce mandat, sans musique, enregistrement ou échantillon tiers. Ils sont stylisés et ne sont pas présentés comme des prises de son réalistes.

- [Manifeste source](../../resources/fishing/audio-manifest.json) : identifiants, traductions, paramètres et événements.
- [Validation](../../resources/fishing/audio-validation.json) : formats, crêtes, raccords et empreintes.
- [Script reproductible](../../scripts/fishing/render_audio.py).
- [Dossier des WAV](../../resources/sounds/fishing/).

Le fichier physique `resources/sounds/fishing/fishing_bite.wav` se joue avec le chemin relatif `fishing/fishing_bite.wav`. Appliquer la même convention à tous les identifiants ci-dessous.

| Famille | Identifiants des 32 ressources |
| --- | --- |
| Interface | `ui_open`, `ui_close`, `ui_select`, `ui_confirm`, `ui_error` |
| Outils | `cast_rod`, `cast_net`, `cast_cage`, `crate_raise`, `net_retrieve` |
| Pêche | `splash`, `bite`, `reel_loop`, `tension_loop`, `line_break`, `escape`, `catch` |
| Compétences | `skill_stabilize`, `skill_pull`, `skill_repair`, `skill_sink` |
| Automatique | `auto_start`, `auto_stop`, `auto_pause` |
| Légendaires | `legendary_appear`, `legendary_challenge_cue`, `legendary_challenge_success`, `legendary_challenge_fail`, `legendary_catch` |
| Progression | `xp_gain`, `level_up` |
| Ambiance | `ambience_sea_loop` |

`crate_raise` désigne la remontée de la cage de pêche. Les événements comme outil absent, inventaire plein, appâts épuisés, timeout et invitation légendaire utilisent les correspondances déclarées dans le manifeste.

## Quatre portées et repli

La configuration runtime résout une clé dans cet ordre : **prise → outil équipé → zone → global → défaut du catalogue**. Dans l’éditeur, présenter ces quatre portées et la provenance de la valeur effective.

- Clé absente d’une portée : poursuivre la recherche dans la portée moins précise.
- Clé présente avec `File` vide : silence explicite ; ne pas réactiver un son par défaut.
- Clé présente avec un fichier : utiliser cette définition de son.
- Aucune définition trouvée : employer le défaut du catalogue, si cet événement en possède un.

Le manifeste du pack possède son propre format descriptif, avec `sound_id`, `enabled` et des gains normalisés. La configuration du moteur utilise notamment `FishingSoundCue.File`, `Volume` de 0 à 100, `Loop`, `CooldownMs`, `MaximumInstances`, `Spatial` et `Range`. Importer les valeurs avec une conversion explicite ; ne pas supposer les deux formats interchangeables. Les options supplémentaires du manifeste, comme le pitch ou les bus, constituent des paramètres proposés tant que leur raccordement n’est pas confirmé.

## Alias du catalogue runtime

Le [catalogue partagé](../../Framework/Intersect.Framework.Core/Fishing/FishingSoundCatalog.cs) reste la référence de code. Les alias principaux associent :

| Clé runtime | Ressource |
| --- | --- |
| `cast` | `cast_rod`, `cast_net` ou `cast_cage`, selon l’outil |
| `reel` / `tension` | `reel_loop` / `tension_loop` |
| `ambience` | `ambience_sea_loop` |
| `legendary` | `legendary_appear` |
| `challenge` | `legendary_challenge_cue` |
| `challenge_success` / `challenge_fail` | `legendary_challenge_success` / `legendary_challenge_fail` |

Le pack expose 58 bindings ; le catalogue de cette intégration expose 68 clés, alias et signaux d’arrêt compris. Le sélecteur de l’éditeur se construit à partir du catalogue de code. Le nombre de clés ne démontre pas leur déclenchement : la recette doit établir les correspondances et raccordements effectifs.

Les clés `challenge_timing_prompt` et `challenge_reel_prompt` sont réservées : le défi intégré utilise les directions. Elles restent prévisualisables mais leurs réglages sont désactivés dans l’éditeur. `splash` est silencieux par défaut pour éviter de doubler le contact déjà inclus dans le lancer ; une surcharge explicite peut l’activer.

## Déclenchement et nettoyage

Une boucle démarre à l’entrée dans son état et s’arrête à sa sortie. Ne pas la redémarrer à chaque mise à jour réseau. Les événements `reeling_stop`, `line_tension_exit`, `session_end` et `session_interrupted` définissent les arrêts attendus dans le pack. Nettoyer aussi les boucles lors de la fermeture de fenêtre ou de la destruction du propriétaire sonore.

Les lancers et récupérations peuvent être spatialisés dans le monde ; les contrôles et défis restent locaux au joueur. Éviter la double lecture locale et répliquée. Les lancers contiennent déjà une petite éclaboussure : ne pas superposer systématiquement un deuxième `splash`. Regrouper les récompenses d’XP pour éviter une accumulation de sons par objet. Toujours doubler les consignes de défi par un signal visuel.

## Format et recette

WAV PCM 16 bits à 44 100 Hz, mono pour les effets et stéréo pour la mer. Crêtes maximales : −6 dBFS pour les ponctuels, −12 dBFS pour moulinet/tension, −18 dBFS pour l’ambiance. Les boucles durent respectivement 2 s, 2,4 s et 12 s. Leur raccord croisé est incorporé ; lire toute la boucle et appliquer les fondus uniquement aux transitions d’état.

Les 32 fichiers passent la vérification numérique : aucun écrêtage, extrémités nulles des ponctuels et raccords continus. Une régénération séparée dans le même environnement a produit les mêmes empreintes. **Aucune écoute réelle n’a été effectuée lors de la synthèse.** Vérifier le mixage dans le client Windows, les préférences de volume, les répétitions, les arrêts et les interactions multijoueurs avant de considérer l’audio comme validé en jeu.
