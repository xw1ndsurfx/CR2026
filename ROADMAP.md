# Corps Royaux — Roadmap 2026–2027

> **Statut : planification interne — revue technique le 7 novembre 2026 (calendrier établi le 2 novembre).**
> Les dates de playtest et de bêta sont des **objectifs**, et non des ouvertures déjà configurées dans Steamworks. Leur maintien dépend des validations techniques. Les volumes indiquent des **joueurs ciblés / invités**, pas un nombre de connexions simultanées garanti.

Cette feuille de route accompagne le développement de **Corps Royaux**, MMORPG 2D de Studio Logiklik, et son moteur personnalisé **Logiklik 2D Engine**, basé sur Intersect. Elle sépare les tests fermés (Steam Playtest), la démo destinée au Steam Next Fest et la bêta ouverte du jeu principal.

[Retour au README](README.md) · [État technique et reprise du projet](PROJECT_STATUS.md) · [Documentation de la stabilité serveur](docs/server-crash-diagnostics.md) · [Documentation du tutoriel](docs/tutorial-guide.md)

## État technique au 7 novembre 2026

La synthèse datée et les preuves GitHub (PR fusionnées, PR ouvertes, limites de validation et plan de reprise) sont détaillées dans **[PROJECT_STATUS.md](PROJECT_STATUS.md)**. Cet état est un instantané : recontrôler `main`, la CI et les PR avant une fusion ou un playtest.

| Statut GitHub | Travail récent | Suite nécessaire |
| --- | --- | --- |
| **Fusionné dans `main`, mais désactivé par défaut** | Combat PvE avancé, phases 1 à 7 ([#238](https://github.com/xw1ndsurfx/CR2026/pull/238), [#241–246](https://github.com/xw1ndsurfx/CR2026/pull/246)) : esquive, parade, riposte, télégraphes, endurance, combos et exercices. | Tests en jeu sur staging, migrations et activation contrôlée de `Combat.EnableAdvancedCombat`. |
| **Fusionné dans `main`, validation à compléter** | Pêche, profession/éditeur et mode auto rééquilibré ([#218](https://github.com/xw1ndsurfx/CR2026/pull/218), [#227](https://github.com/xw1ndsurfx/CR2026/pull/227)). | Régression sessions réelles, prises, EXP et équipement. |
| **Fusionné en partie ; PR ouverte** | Exportation, éditeur unifié, métier et ajustements UI ([#228](https://github.com/xw1ndsurfx/CR2026/pull/228), [#239](https://github.com/xw1ndsurfx/CR2026/pull/239)); nouvelle interface [#240](https://github.com/xw1ndsurfx/CR2026/pull/240) **non fusionnée**. | Tester livraison/naufrage et rendu visuel ; examiner #240. |
| **Fusionné dans `main`, validation à compléter** | Commandes d'artisanat, panneau via Event, GFX/SFX et habillage ([#230](https://github.com/xw1ndsurfx/CR2026/pull/230), [#237](https://github.com/xw1ndsurfx/CR2026/pull/237)). | Recette à deux joueurs et résilience des sauvegardes. |
| **PR en brouillon, non fusionnées** | Royal Guide visuel : [#247](https://github.com/xw1ndsurfx/CR2026/pull/247) → [#248](https://github.com/xw1ndsurfx/CR2026/pull/248) → [#249](https://github.com/xw1ndsurfx/CR2026/pull/249). | Compilation, essais UI/serveur, intégration ordonnée après revue. |

**Règle de suivi :** `PR fusionnée` ≠ `jeu testé` ≠ `jeu déployé`. Les cases de recette ci-dessous restent ouvertes tant qu'aucune preuve d'acceptation correspondante n'a été consignée.

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

## Calendrier marketing — novembre 2026 à mars 2027

> **Plan révisé le 2 novembre 2026.** La préparation initialement prévue les 22–31 octobre est réorganisée à partir du 2 novembre; les anciennes dates ne sont pas considérées comme des actions accomplies. Le **7 décembre est le lancement du Playtest communautaire #2, pas l'Open BETA**. Les campagnes suivantes restent conditionnelles à la validation technique et aux autorisations Steamworks.

| Dates | Phase | Actions prioritaires de Studio Logiklik / Work | Message et appel à l'action |
| --- | --- | --- | --- |
| **2–8 novembre 2026** | **Préparation accélérée** | Auditer page Steam/site/communautés et jeux comparables; constituer un press kit (logo, captures authentiques, présentation, contact); vérifier l'état des builds; préparer 3–4 semaines de contenus FR/EN; lister créateurs et médias; définir des indicateurs de référence | **Découvrir Corps Royaux**, ajouter à la **wishlist Steam**; annoncer le premier test uniquement si le dispositif d'accès est prêt |
| **9–15 novembre 2026** | **Début de la promotion + Playtest fermé #1 (14–15 nov.)** | Publier présentation de l'univers Polonya/Herbonia et extrait de gameplay réel; recruter **20–50 joueurs ciblés**; envoyer les invitations autorisées; communiquer les instructions et horaires des sessions; couvrir les tests et recueillir les retours | **Wishlist Steam** + **demande d'accès Playtest** (uniquement si l'accès est effectivement ouvert); ne pas promettre plus de places que prévues |
| **16–30 novembre 2026** | **Intensification / preuve de progression** | Publier le bilan du premier test et les correctifs réels; montrer donjons, invasions, familiers, professions, combat et naval **seulement si démontrables**; préparer shorts/clips, captures et publications Discord, Steam, Reddit et réseaux sociaux; proposer aux créateurs de petites démos ou invitations approuvées | Développer les **wishlists** et les inscriptions au **Playtest #2** du 7 décembre |
| **1er–7 décembre 2026** | **Campagne événementielle Playtest #2** | Teaser / bande-annonce avec images du jeu; compte à rebours du 3 au 7 décembre; détails d'accès, prérequis, fuseaux horaires, règles et support; animation communautaire et état des serveurs pendant le test; ouverture progressive pour **100–300 joueurs ciblés** selon capacité réelle | **Participer au Playtest communautaire du 7 décembre** et ajouter le jeu à sa wishlist; **ne pas annoncer une Open BETA** |
| **8–31 décembre 2026** | **Fidélisation et crédibilité** | Remercier les participants; synthétiser retours, problèmes et améliorations; publier une mise à jour de développement, clips véridiques et jalons atteints; préparer la liste de testeurs de janvier et le périmètre de la démo | Continuer à **suivre le développement**, rejoindre la communauté et conserver le jeu en wishlist |
| **Janvier 2027** | **Playtest #3 + préparation Next Fest** | Recruter les participants du test de charge; communiquer résultats/optimisations sans promettre des capacités non mesurées; finaliser les arguments et contenus de la démo; actualiser trailer et page Steam; **inscription Next Fest avant le 10 janvier** si admissible; soumissions Valve dans les délais de la section dédiée | Wishlist Steam, retours joueurs et anticipation d'une démo publique **si validée** |
| **1er–21 février 2027** | **Pré-campagne Steam Next Fest** | Vérifier page et build de démo, presse/streamers, bande-annonce et visuels; préparer messages de découverte, calendrier de publications et modération; ne confirmer la participation qu'après acceptation | **Ajouter Corps Royaux à sa wishlist** et découvrir la démo lorsque sa disponibilité est confirmée |
| **22 février–1er mars 2027** | **Steam Next Fest (participation visée)** | Mettre la **démo publique** à disposition après validation; annoncer sa disponibilité, répondre aux retours, alimenter FAQ et contenus de gameplay; compiler statistiques et demandes de correction | **Jouer à la démo Steam** + wishlist; le Steam Playtest reste un produit distinct |
| **Mars 2027** | **Préparation / lancement conditionnel Open BETA** | Décision « go / no-go » selon stabilité et résultats du festival; annoncer date et accès **seulement après validation**; organiser communication de lancement, suivi communautaire et mises à jour | **Rejoindre l'Open BETA** seulement lorsqu'elle est effectivement disponible |

### Stratégie éditoriale

Les objectifs et cadences ci-dessous sont **des propositions de travail**, ajustables au temps disponible et à la qualité des assets; ils ne constituent pas des publications déjà planifiées ou effectuées.

- **Novembre (préparation / recrutement) :** viser **2–3 publications utiles par semaine** et **1 vidéo courte** si du gameplay authentique est prêt. Alterner univers, quêtes, professions, combat, coulisses du moteur et accès aux tests.
- **16 novembre–7 décembre :** renforcer progressivement la fréquence pour les contenus qui fonctionnent; préparer **un compte à rebours quotidien du 3 au 7 décembre** uniquement après confirmation du Playtest #2.
- **Après chaque test :** publier un **bilan transparent sous 48–72 h** si les faits sont validés; éviter de montrer comme disponibles des systèmes encore au stade de prototype.
- **Janvier–février :** concentrer la création sur une démonstration jouable et la promotion du Next Fest; adapter les contenus à Steam, créateurs de contenu et communautés d'indie MMORPG.
- **Langues :** contenu marketing prioritairement **français et anglais**; prévoir des adaptations espagnoles vérifiées lorsque pertinentes pour le public et les ressources disponibles.
- **Canaux :** **Steam** (page, actualités, wishlists), **Discord** (communauté/retours), **YouTube** et formats courts, **Facebook/Instagram/TikTok**, **Reddit** selon les règles et la pertinence de chaque communauté. Ne pas publier le même message promotionnel partout sans adaptation.

### Mandat de Work : production et supervision marketing

**Responsable opérationnel visé : ChatGPT Work, sous supervision de Studio Logiklik.** Work prépare les livrables et peut effectuer les actions permises par les sites/outils connectés et les autorisations obtenues; il ne faut pas supposer l'accès automatique à tous les réseaux sociaux ni à toutes les données Steamworks.

- [ ] Réaliser un **audit Steam et de la concurrence**, établir positionnement, promesse, mots-clés, captures et bande-annonce à corriger.
- [ ] Constituer un **press kit exploitable** : présentation courte/longue FR/EN, logo et captures réellement autorisés, fiche technique, dates **prévisionnelles**, coordonnées officielles.
- [ ] Livrer un **calendrier éditorial hebdomadaire** avec textes/visuels/vidéos, canal, cible, CTA, date, statut et personne responsable de l'approbation.
- [ ] Rechercher journalistes, streamers et créateurs (principalement indie/MMORPG), classer les contacts pertinents, préparer des messages personnalisés et proposer un ordre de prise de contact.
- [ ] Préparer les **annonces d'ouverture, instructions et comptes rendus** de chaque playtest; ne pas diffuser de détails de connexion ou de serveurs non confirmés.
- [ ] Préparer les éléments marketing et la revue des assets nécessaires au **Steam Next Fest** dès janvier, sous réserve de l'éligibilité.
- [ ] Suivre **chaque semaine** wishlists Steam (si données accessibles), visites/UTM, demandes d'accès, invitations, connexions effectives, retours exploitables, réponses des créateurs et performances des publications.
- [ ] Réaliser une revue des résultats après chaque test; réaffecter les efforts vers les contenus qui attirent des joueurs réellement intéressés.

**Contrôle humain obligatoire :** ne pas publier, contacter un créateur ou un journaliste, promettre un accès, changer une date publique, acheter de la publicité, engager une dépense ou ouvrir le test sans approbation explicite du responsable. Les publications et suivis sont des tâches à réaliser, **pas des actions déjà exécutées**.

### Indicateurs et critères d'ajustement

| Indicateur | Utilité | Source / réserve |
| --- | --- | --- |
| **Nouvelles wishlists / évolution hebdomadaire** | Objectif principal de découverte du jeu | Steamworks si l'accès aux statistiques est disponible; conserver une mesure de départ |
| **Visites page Steam et canaux d'origine** | Comprendre quels contenus attirent le bon public | Statistiques Steam / liens UTM lorsque disponibles |
| **Demandes d'accès, invitations, joueurs présents** | Vérifier si la campagne convertit en joueurs tests | Steamworks / métriques serveur, en distinguant inscriptions et joueurs simultanés |
| **Retours exploitables et bogues critiques** | Décider du contenu montré et de l'étendue du test suivant | Formulaire/support et journaux serveur |
| **Réponses des médias/créateurs et contenu publié** | Mesurer la portée des partenariats | Tableau de prospection et liens réels, sans affirmer un accord non obtenu |

**Décisions de pilotage :** si la stabilité est insuffisante, **reporter ou réduire le playtest**, prévenir les participants et remplacer l'annonce de lancement par une mise à jour transparente; ne pas accroître les inscriptions au-delà de la capacité de test. Le planning marketing suit le développement, pas l'inverse.

## Rythme de pilotage

- **Chaque semaine :** revue développement, sécurité/stabilité, expérience joueur, marketing et risques.
- **Après chaque test :** rapport avec observations mesurées, causes connues, tâches assignées et décision d'élargissement.
- **Avant publication :** vérification des licences/assets, traduction, sauvegardes, conformité Steam et approbation du responsable.
- **Après le 1er mars 2027 :** reconfirmer ou reporter l'Open BETA de mars selon la qualité constatée; éviter une date « ferme » avant cette décision.

*Ce roadmap décrit des engagements de planification, pas des résultats de tests réalisés. Les cases ne doivent être cochées que lorsque les preuves correspondantes existent.*
