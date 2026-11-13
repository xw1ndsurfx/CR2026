# Corps Royaux — état du développement et reprise de contexte

> **Suivi actualisé le 11 novembre 2026**, à partir de `main`, des PR vérifiées et des retours utilisateur. Ce document n'est ni une certification de jouabilité, ni une promesse de lancement. Les PR, branches et états CI peuvent changer après cette date.

**Sources principales :** [README](README.md) · [ROADMAP](ROADMAP.md) · [règles de contribution et de traduction](AGENTS.md) · [diagnostic serveur](docs/server-crash-diagnostics.md).

## Référence et architecture

- **Projet :** MMORPG 2D médiéval-fantastique Corps Royaux (Polonya / Herbonia), Studio Logiklik.
- **Dépôt de travail :** `xw1ndsurfx/CR2026` ; **branche de référence :** `main`. Dernier commit de fusion observé pour cet état : [PR #265](https://github.com/xw1ndsurfx/CR2026/pull/265), `69670d777`. Le client Intel HD 3000 reste sur une branche locale séparée et ne doit pas être fusionné dans `main`.
- **Moteur :** Logiklik 2D Engine, personnalisation d'Intersect ; solution `Intersect.sln`, C# et .NET 8.
- **Applications :** client, serveur, éditeur Windows et bibliothèques partagées. Les modifications du protocole exigent des versions compatibles.
- **Localisation :** EN/FR/ES obligatoire pour tout nouveau texte joueur selon [AGENTS.md](AGENTS.md).
- **Document maître des échéances :** [ROADMAP.md](ROADMAP.md) ; toutes les dates de playtest y sont **prévisionnelles**.

## État des fonctionnalités — distinction code / validation

| Chantier | État observé au 11 novembre | Validation encore nécessaire |
| --- | --- | --- |
| **Agriculture de la maison** | Première implémentation intégrée ([#195](https://github.com/xw1ndsurfx/CR2026/pull/195)), métier Agriculteur, cultures et ouvriers. Fonctionnement confirmé par l’utilisateur le 11 novembre. [Guide](Documentation/features/farming.md). | Consigner une recette couvrant absence, redémarrage, réservations de graines/salaires, capacité du grenier et droits du propriétaire ; le retour fonctionnel ne précise pas ces scénarios. |
| **Client Intel HD 3000** | Correctif audio local installé ; client testé selon le retour utilisateur du 11 novembre. La bibliothèque native mise à jour est embarquée dans le client privé. Le client normal et l’éditeur sont préservés. | Essai prolongé en jeu sur ce laptop ; distribution privée à conserver séparée des builds normaux. |
| **Ressources et imports ; abandon de donjon** | Correctifs [#264](https://github.com/xw1ndsurfx/CR2026/pull/264) et sélecteur des ouvriers [#265](https://github.com/xw1ndsurfx/CR2026/pull/265) fusionnés. Les quatre workflows de #265 sont réussis. | Vérifier les binaires réellement déployés et la recette en jeu ; le sélecteur modifie seulement l’éditeur. |
| **Combat PvE avancé** (esquive, parade, riposte, télégraphes, endurance, rage de boss, sorts annoncés, combos, exercices) | Phases intégrées dans `main` : [#238](https://github.com/xw1ndsurfx/CR2026/pull/238) et [#241–246](https://github.com/xw1ndsurfx/CR2026/pull/246). Le système reste **désactivé par défaut** via `Combat.EnableAdvancedCombat`. [Guide](Documentation/AdvancedCombat.md). | Activer uniquement en staging après compilation/migrations, vérifier latence, instances, protections serveur, tutoriel et ressources. L'intégration Git n'est pas une validation manuelle en jeu. |
| **Tutoriel de première connexion et exercices de combat** | Guide configurable intégré par [#203](https://github.com/xw1ndsurfx/CR2026/pull/203), exercices d'esquive/parade/riposte/combos par [#246](https://github.com/xw1ndsurfx/CR2026/pull/246). [Guide actuel](docs/tutorial-guide.md). | Reconnexion, progression persistante, reprise, langues et exercices avec combat activé/désactivé. |
| **Royal Guide visuel** (halo, flèches, PNJ, marqueurs au sol, interactions d'événements) | **Pas dans `main` à cette date.** [#247](https://github.com/xw1ndsurfx/CR2026/pull/247), [#248](https://github.com/xw1ndsurfx/CR2026/pull/248), [#249](https://github.com/xw1ndsurfx/CR2026/pull/249) ouvertes en brouillon, empilées. | Compiler client/serveur/éditeur, valider interface et étapes en jeu, puis intégrer dans l'ordre des dépendances. |
| **Pêche** (profession, éditeur, multi-cartes, modes actif/auto, prises, ressources) | Intégrée dans `main` par [#218](https://github.com/xw1ndsurfx/CR2026/pull/218), compléments et correctifs [#219–225](https://github.com/xw1ndsurfx/CR2026/pull/225), équilibrage [#227](https://github.com/xw1ndsurfx/CR2026/pull/227). La pêche auto a, par défaut, 50 % d'échecs et 20 % de l'EXP normale sur succès. [Rapport technique](Documentation/Fishing/Integration-status.fr.md). | Tester à nouveau la configuration active, équipements, sessions réelles, persistance, piles, légendaires et charge ; le retour positif sur l'ouverture du mini-jeu n'est pas une recette exhaustive. |
| **Exportation** (marchands, commandes, métier/EXP, cargo, route navale et quêtes) | Intégration initiale [#228](https://github.com/xw1ndsurfx/CR2026/pull/228) et éditeur unifié [#229](https://github.com/xw1ndsurfx/CR2026/pull/229) dans `main` ; améliorations [#232](https://github.com/xw1ndsurfx/CR2026/pull/232), [#234](https://github.com/xw1ndsurfx/CR2026/pull/234), [#235](https://github.com/xw1ndsurfx/CR2026/pull/235), [#239](https://github.com/xw1ndsurfx/CR2026/pull/239) fusionnées. La nouvelle interface de préparation/livraison [#240](https://github.com/xw1ndsurfx/CR2026/pull/240) est **ouverte en brouillon**, non fusionnée. | Essai avec bateau possédé non coulé, embarquement réel, naufrage, reprise de cargaison, livraison et EXP ; vérifier GFX, lisibilité jour/nuit et résolution avant #240. |
| **Commandes d'artisanat** (panneau via Event, UI, GFX/SFX) | Implémentation [#230](https://github.com/xw1ndsurfx/CR2026/pull/230), ressources [#231](https://github.com/xw1ndsurfx/CR2026/pull/231), correction crash [#233](https://github.com/xw1ndsurfx/CR2026/pull/233), lisibilité [#236](https://github.com/xw1ndsurfx/CR2026/pull/236), atlas et habillage [#237](https://github.com/xw1ndsurfx/CR2026/pull/237) fusionnés. [Documentation artistique](Documentation/CraftingOrders/RoyalArtworkIntegration.fr.md). | Parcours complet à deux personnages : publier, accepter, fabriquer, livrer, payer, annuler/expirer ; vérifier publication des assets et sauvegarde en panne. |
| **Roll the Dice** | Mini-jeu et salon multijoueur fusionnés via [#216](https://github.com/xw1ndsurfx/CR2026/pull/216). | Essais réels de mise, récompenses, EXP, reconnect et plusieurs joueurs. |
| **Consommables : grog / effets temporaires** | Effets progressifs et modificateurs de statistiques fusionnés via [#226](https://github.com/xw1ndsurfx/CR2026/pull/226). | Équilibrage, durée/cooldown, animations et non-régression multijoueur. |
| **Événements, donjons, naval** | Divers correctifs intégrés : boss mondiaux/invasions/sorties de donjon [#221](https://github.com/xw1ndsurfx/CR2026/pull/221), embarquement et reconnexion aux donjons [#223](https://github.com/xw1ndsurfx/CR2026/pull/223). D'autres prototypes figurent dans le [README](README.md). | Scénarios multijoueurs, persistance, pertes réseau et charge serveur avant de les annoncer comme finalisés. |

**Légende :** « fusionné dans `main` » = code présent dans l'historique de cette branche ; cela ne prouve ni déploiement, ni compilation du build de production, ni acceptation QA. Les PR ouvertes ne doivent pas être comptées comme livrées.

## Pull requests ouvertes vérifiées le 11 novembre

1. [#240 — Exportation : nouvelle interface](https://github.com/xw1ndsurfx/CR2026/pull/240), **draft**, cible `main` ; vérifier builds + rendu réel.
2. [#247 — Royal Guide phase 1](https://github.com/xw1ndsurfx/CR2026/pull/247), **draft**, cible `main` ; halo et contrôles HUD.
3. [#248 — Royal Guide phase 2](https://github.com/xw1ndsurfx/CR2026/pull/248), **draft**, basée sur la branche #247 ; guidage des PNJ, cases et éditeur.
4. [#249 — Royal Guide phase 3](https://github.com/xw1ndsurfx/CR2026/pull/249), **draft**, basée sur la branche #248 ; interactions d'événements vérifiées par serveur.

**Ordre d'intégration du guide :** #247 puis #248 puis #249, en tenant compte des tests, des mises à jour de branches et des conflits éventuels. Ne pas fusionner directement des branches historiques dont le travail est déjà sur `main`.

## Reprise prioritaire et critères avant playtest

**Choix confirmé par l’utilisateur le 11 novembre : stabilité et préparation du playtest.** Voir le [rapport de préparation](Documentation/Playtests/Readiness-2026-11-11.fr.md). Les tests locaux utilisent des données isolées ; ils ne prouvent pas une sauvegarde restaurable ni une authentification Steam fonctionnelle sur le serveur déployé.

La recette locale de reconnexion et de redémarrage avec un joueur est [consignée le 11 novembre](Documentation/Playtests/Reconnexion-2026-11-11.fr.md) : persistance des états enregistrés confirmée ; gains et déplacement non expliqués après connexion à diagnostiquer.

L’[enquête instrumentée au repos](Documentation/Playtests/Repos-2026-11-11.fr.md) observe une réapparition après mort sans entrée de déplacement ; le gain initial de 20 EXP reste non reproduit et non expliqué.

### P0 — sécurité et première boucle de jeu

- [ ] Vérifier la révision exacte du serveur, du client, de l'éditeur et des données ; exécuter compilations et suites CI sur cette révision, puis noter les résultats.
- [ ] Tester avec des comptes propres l'entrée Steam / Logiklik, personnage, inventaire, combat de base, quête, reconnexion et persistance.
- [ ] Tester crash/redémarrage, journaux, restauration de sauvegarde, inventaire et transactions ; ne pas publier de données sensibles.
- [ ] Décider « go / no-go » pour le playtest fermé des **14–15 novembre 2026** ; cette date demeure une cible, pas une session confirmée.

### P1 — vérifications des fonctionnalités récentes

- [ ] Compléter et tester #240 (Exportation) ; contrôler la chaîne commande → navire → livraison ou naufrage → récompenses.
- [ ] Vérifier l'ensemble Royal Guide (#247–249) avec objectifs client/serveur, événements et compatibilité des anciens tutoriels.
- [ ] Effectuer le scénario complet des Commandes d'artisanat avec deux joueurs.
- [ ] Rejouer la Pêche automatique à différentes probabilités et valider l'EXP, la configuration active et les prises.
- [ ] Déployer le combat avancé **uniquement en staging** derrière son option, après les migrations requises ; tester mobs, boss, sorts, combos et exercices dans différents cas réseau.

### P2 — pistes de conception, non engagées comme fonctionnalités livrées

Les idées discutées pour une étape ultérieure comprennent les **expéditions navales coopératives**, **configurations rapides d'équipement**, **quartier général de guilde**, **matchmaking amélioré**, **courses de montures**, **donjons à modificateurs** et **saisons royales**. Les maquettes et idées ne signifient pas que ces fonctionnalités sont intégrées à `main`. Priorité, périmètre et faisabilité sont à valider séparément.

## Reprise sur une nouvelle session de développement

1. Ouvrir le dépôt et lire `README.md`, `ROADMAP.md`, `PROJECT_STATUS.md`, `AGENTS.md` et la documentation du module concerné.
2. Partir de `main`, mettre à jour les sous-modules (`git submodule update --init --recursive`), examiner les PR ouvertes, puis créer une **nouvelle branche** pour chaque correction. Ne pas supposer qu'un ancien titre de PR prouve un résultat en production.
3. Conserver la séparation source / validation : revue du diff, tests automatisés, compilation client/serveur/éditeur, essais multijoueurs puis décision de fusion et de déploiement.
4. Vérifier les migrations, binaires, configurations, ressources, droits de licence et sauvegardes **hors Git** avant toute opération serveur. GitHub ne sauvegarde pas la base de production, les secrets, les assets absents du dépôt ou les fichiers locaux ignorés.
5. Pour les dates de lancement et annonces publiques, suivre uniquement les jalons conditionnels de [ROADMAP.md](ROADMAP.md) et la décision du responsable du studio.

**Protection :** ne jamais ajouter de mot de passe, jeton, clé Steam/Logiklik, sauvegarde de compte/joueur ou donnée personnelle à ce dépôt public.
