# Corps Royaux — Roadmap 2026–2027

> **Statut : planification interne — mis à jour le 2 novembre 2026.**
> Les dates de playtest et de bêta sont des **objectifs**, et non des ouvertures déjà configurées dans Steamworks. Leur maintien dépend des validations techniques. Les volumes indiquent des **joueurs ciblés / invités**, pas un nombre de connexions simultanées garanti.

Cette feuille de route accompagne le développement de **Corps Royaux**, MMORPG 2D de Studio Logiklik, et son moteur personnalisé **Logiklik 2D Engine**, basé sur Intersect. Elle sépare les tests fermés (Steam Playtest), la démo destinée au Steam Next Fest et la bêta ouverte du jeu principal.

[Retour au README](README.md) · [Documentation de la stabilité serveur](docs/server-crash-diagnostics.md) · [Documentation du tutoriel](docs/tutorial-guide.md)

## Calendrier de diffusion et de tests

| Période | Étape | Audience cible | Objectif principal | Condition de passage |
| --- | --- | --- | --- | --- |
| **2–13 novembre 2026** | Préparation du premier test | Équipe + petit groupe QA | Stabilisation, build Steam, recrutement, protocole de test, support et collecte de bogues | Installation et connexion Steam, personnages, sauvegardes, progression et serveur vérifiés |
| **14–15 novembre 2026** | **Playtest fermé #1** | **20 à 50 joueurs ciblés** | Authentification Steam/Logiklik, création de personnage, mouvement, combat, reconnexion, persistance et stabilité | Aucun bogue critique bloquant, incidents reproductibles consignés et plan de correction |
| **16 novembre–6 décembre 2026** | Correctifs + campagne communautaire | Testeurs + public intéressé | Corriger les régressions, préparer contenu et annonces, recruter pour le test suivant | Validation des fonctions proposées pour le second playtest |
| **7 décembre 2026** | **Playtest communautaire #2** | **100 à 300 joueurs ciblés**, accès progressif | Jouabilité multijoueur, invasions, donjons, guildes, familiers et naval **uniquement si ces modules sont suffisamment stables** | Mesures de charge réelles, pas de perte de données, boucle de jeu principale satisfaisante |
| **Janvier 2027** | **Playtest #3 / test de charge** | Groupe adapté à la capacité démontrée | Concurrence, réseau, CPU/RAM, logs de crash, persistance, équilibrage et corrections | Serveur et jeu prêts pour une démo publique, selon une revue « go / no-go » |
| **22 février–1er mars 2027** | **Steam Next Fest — participation visée** | Public Steam | Démo **publique et distincte du Steam Playtest**, visibilité, retours joueurs, wishlists | Admissibilité Steamworks confirmée, inscription et démo approuvées, jeu principal non sorti avant la fin du festival |
| **Mars 2027** | **Open BETA — objectif** | Audience plus large | Déploiement progressif du jeu principal, suivi des performances et amélioration continue | Critères de lancement atteints; date à confirmer après les tests et Next Fest |

**Important :** le Steam Playtest ne constitue pas une sortie commerciale du jeu principal. La date de sortie de l'application principale dans Steamworks doit être revue séparément; **ne pas mettre le jeu principal en vente / en sortie publique avant le Steam Next Fest** si la participation de février 2027 est maintenue.

## Portes de validation par étape

### Playtest fermé #1 — 14 et 15 novembre 2026

- [ ] Distribuer un client et un serveur **issus de la même révision**, avec configuration et sauvegardes vérifiées.
- [ ] Vérifier l'authentification Steam puis la connexion au compte Logiklik.
- [ ] Tester création/sélection de personnage, entrée dans le monde, déplacements entre cartes et reconnexion.
- [ ] Vérifier combat, inventaire, quêtes élémentaires, sauvegarde et progression après redémarrage.
- [ ] Simuler déconnexion, mort, erreur de réseau et reprise du serveur sans duplication ni perte de progression.
- [ ] Conserver les journaux console et crash, relever les bogues P0/P1 et documenter les étapes de reproduction.
- [ ] Distribuer un questionnaire EN/FR/ES et un canal de signalement clairement identifié.

**Livrable :** compte rendu #1 (tests effectués, incidents, correctifs prioritaires, décision sur le second test).

### Playtest communautaire #2 — 7 décembre 2026

- [ ] Valider les régressions du premier test, y compris les migrations de données et l'arrêt/reprise du serveur.
- [ ] Ouvrir l'accès par vagues pour mesurer la charge **avant** d'élargir le groupe; ne pas confondre inscriptions et joueurs simultanés.
- [ ] Préparer une boucle de découverte : tutoriel, progression, groupe, activité et récompense.
- [ ] Sélectionner les systèmes démontrables, conditionnés à la stabilité : invasions, donjons, guildes, familiers et éventuellement naval.
- [ ] Tester commandes d'administration, modération, support et communication pendant un incident.
- [ ] Mesurer les connexions réussies, déconnexions anormales, erreurs serveur, charge et retours des joueurs.

**Livrable :** bilan communautaire #2 et liste de corrections/optimisations pour janvier.

### Playtest #3 — janvier 2027

- [ ] Réaliser une montée en charge progressive, en surveillant simultanément serveur, réseau, base de données et client.
- [ ] Tester la persistance, la récupération après crash et l'intégrité des comptes/personnages.
- [ ] Rejouer les scénarios critiques : connexion, progression, instances, événements mondiaux et redémarrage.
- [ ] Valider traduction et ergonomie EN/FR/ES, première connexion, accessibilité des contrôles et support.
- [ ] Décider si une **démo réduite, cohérente et stable** peut être soumise au Steam Next Fest.

**Livrable :** décision documentée « go / no-go » pour la démo.

## Steam Next Fest — février 2027

Échéances publiées par Valve pour l'édition du **22 février au 1er mars 2027** :

| Échéance Valve | Action prévue |
| --- | --- |
| **10 janvier 2027, 23 h 59 PST** | Inscription au Next Fest dans Steamworks (si admissible) |
| **18 janvier 2027** | Soumettre démo + page pour examen si participation souhaitée à l'avant-première presse |
| **1er février 2027** | Date limite de soumission des éléments requis pour examen Valve |
| **11 février 2027** | Début de l'avant-première presse pour les démos inscrites |
| **22 février 2027** | Démo publiquement jouable au lancement du festival |
| **1er mars 2027** | Fin du festival ; analyse de la participation et décision sur l'Open BETA |

**Admissibilité à confirmer :** compte Steamworks en règle, page du jeu visible, titre toujours « à venir », démo publique opérationnelle, exigences actuelles de Valve respectées. La démo doit avoir un périmètre délibérément limité; **un Steam Playtest n'est pas, à lui seul, la démo exigée**. Un jeu ne peut participer qu'une fois au Next Fest.

Source officielle : [Steamworks — Next Fest, février 2027](https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest/feb_2027). Recontrôler ces règles et les échéances avant soumission.

## Chantier moteur et contenu — priorités de validation

Ces éléments sont des **domaines de travail**, pas une affirmation que chaque fonctionnalité est finalisée ou déjà validée en production.

| Priorité | Chantier | Critère de préparation |
| --- | --- | --- |
| **P0** | Stabilité serveur, migrations, journaux, sauvegardes et reprise | Aucun crash bloquant connu pour le scénario testé; diagnostics exploitables |
| **P0** | Connexion Steam / Logiklik, personnages et persistance | Parcours d'entrée de jeu reproductible sur des comptes de test propres |
| **P0** | Réseau, déconnexions, transactions et sécurité des récompenses | Pas de perte de progression ou d'exploit évident sur les parcours testés |
| **P1** | Tutoriel et expérience de première connexion | Guide jouable et traductions EN/FR/ES vérifiées |
| **P1** | Donjons, invasions, événements et progression | Contenus retenus testés en sessions multijoueurs |
| **P1** | Interface, navigation et outils de l'éditeur | Régression des workflows essentiels du Logiklik 2D Engine |
| **P1** | Démo Steam Next Fest | Une boucle de jeu autonome, compréhensible et stable, avec configuration de publication distincte |
| **P2** | Naval, joutes, cinématiques, concours d'archerie, montures, maisons | Périmètre publié défini par la validation en conditions réelles, sans annoncer un prototype comme terminé |
| **P2** | Équilibrage, économie, artisanat, compagnons et systèmes sociaux | Économie/XP cohérentes, abus et exploitations prioritaires corrigés |

Documents techniques existants :

- [Joutes : audit et limites](Documentation/Jousting/AUDIT.md)
- [Royal Cinematic Director : architecture](Documentation/RoyalCinematicDirector/Architecture.md)
- [Royal Weapon Mastery / Archery : état et travaux restants](Documentation/RoyalCombat/README.md)
- [Montures : prototype](docs/mount-prototype.md)
- [Maisons et mobilier](Documentation/features/housing.md)
- [Familiers](Documentation/features/pets.md)
- [Brouillard d'exploration](docs/world-map-exploration.md)
- [Outil de diagnostic des crashes](docs/server-crash-diagnostics.md)

## Promotion et communauté — en continu dès novembre 2026

**Responsable opérationnel visé : ChatGPT Work, sous supervision de Studio Logiklik.** Les actions de publication, contact externe, publicité payante et engagement financier demandent une approbation humaine préalable.

- [ ] Auditer la page Steam : texte, langues, visuels, bande-annonce, actualités et demande d'accès au Playtest.
- [ ] Préparer un press kit, la proposition de valeur, une FAQ et des contenus courts en français et en anglais.
- [ ] Planifier les annonces avant/après chaque Playtest et partager les correctifs réellement livrés.
- [ ] Identifier les petites et moyennes communautés MMORPG/indie ainsi que les créateurs pertinents.
- [ ] Préparer des messages personnalisés et les faire approuver avant tout envoi.
- [ ] Centraliser signalements de bogues, suggestions, questionnaires et résumés de retours.
- [ ] Suivre chaque semaine les wishlists, demandes d'accès, joueurs réellement connectés, participation aux tests, retours et incidents (seulement lorsque les données sont accessibles).
- [ ] Préparer une campagne spéciale Next Fest si l'inscription est acceptée.

## Rythme de pilotage

- **Chaque semaine :** revue développement, sécurité/stabilité, expérience joueur, marketing et risques.
- **Après chaque test :** rapport avec observations mesurées, causes connues, tâches assignées et décision d'élargissement.
- **Avant publication :** vérification des licences/assets, traduction, sauvegardes, conformité Steam et approbation du responsable.
- **Après le 1er mars 2027 :** reconfirmer ou reporter l'Open BETA de mars selon la qualité constatée; éviter une date « ferme » avant cette décision.

*Ce roadmap décrit des engagements de planification, pas des résultats de tests réalisés. Les cases ne doivent être cochées que lorsque les preuves correspondantes existent.*
