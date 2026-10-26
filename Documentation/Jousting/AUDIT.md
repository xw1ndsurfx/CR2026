# Joutes royales — audit et plan d’intégration

Base auditée : 4f20480 (main, 25 octobre 2026). La spécification complète V2.0 est conservée dans Conception-v2.txt. La demande de développement actuelle autorise son implémentation progressive.

## Constats vérifiés
- MountConfiguration / MountAnimationClock : feuilles quatre directions, phases Start/Loop/End, cellules configurables. William inclut déjà cavalier et cheval : ajouter un mode de rendu intégré sans second paperdoll.
- MapOptions : grille par défaut 32x32 ; aucun config.json de production livré dans ce dépôt. Ne pas annoncer que la grille du serveur de production a été vérifiée.
- CinematicScene / CinematicCompiler : mouvements natifs, acteurs temporaires, caméra, projection Perspective2_5D, présentation, quêtes et Events. Réutiliser ces ressources ; aucun moteur de caméra parallèle.
- Player.EnqueueStartCommonEvent permet des raccordements aux scènes compilées par le Director via les Events existants.
- Réseau MessagePack avec paquets dédiés client/serveur. Le client transmet une intention de visée, jamais le score ni l’heure d’impact.
- Localisation obligatoire en/fr/es (AGENTS.md).
- Archives Joutes et Joutes(1) : roi, Gisabelle, William, Night, GIFs ; pas de licence jointe. Utilisation locale autorisée par la demande, pas de republication publique des planches source sans preuve de droits.
- Overworld_Objects.png (272x208) et Overworld_Buildings.png (544x544) ont ensuite été fournis depuis le dossier local de l’utilisateur. Les meubles, bâtiments du désert et autres planches accessibles ont aussi été examinés. Certains fichiers OneDrive ne sont pas disponibles localement.
- Correction prioritaire confirmée par l’utilisateur : tuiles natives de 16x16 ET échelle des objets identique aux planches existantes. La valeur de grille par défaut du code ne doit pas servir à agrandir ces assets. Aucun changement à la configuration de production.
- Maquette 150159 : direction artistique et composition ; capture 150204 : correspond à la référence Overworld_Objects. Le nouveau décor doit être livré comme fichiers exploitables et pas comme image-concept.

## Plan et portes de validation
1. Audit : spécification, inventaire avec dimensions/hash/transparence, références conservées, formats vérifiés.
2. Première tranche PvE : configuration d’arène, moteur de session déterministe, IA à cinq difficultés, visée bornée, trois passes configurables, résolution simultanée, abandon et nettoyage. Interface jouable et intégration réseau. Tests de frontières et entrées malveillantes avant validation.
3. Graphismes : génération de vues rapprochées, atlas et ancrages ; importer localement William, roi et Gisabelle. Ne qualifier de complet aucun atlas sans vérification des cellules, de la grille et du rendu dans l’éditeur et en jeu.
4. PvP : invitations consenties, horloge partagée, isolation des sessions, déconnexions ; validation deux clients indispensable.
5. Gradins : réservation atomique, présence réelle et animations assises visibles sur deux clients, priorité joueurs sur PNJ, libération sur sortie.
6. Éditeur : réglages et placement visuel, sélection des ressources Cinematic existantes, test PvE.
7. Cérémonies et progression : Events, quêtes, achievements, récompenses idempotentes, wiki ; aucun avantage Premium.
8. Tournois : classement saisonnier et tableau après validation du duel.

## État de validation
L’audit et le code ne constituent pas une validation en jeu. Les critères du chapitre 15 restent ouverts jusqu’aux preuves correspondantes. Les essais automatisés, compilation, visuels inspectés et limites réelles sont enregistrés séparément.

## Avancement de la tranche PvE

L’adversaire est maintenant une entité temporaire de la carte partagée utilisant le rendu des montures. Les deux trajectoires suivent la même horloge jusqu’au milieu de la lice et au retour. Le Director natif fournit les cinq sources de séquence avec raccordement depuis le Jousting Editor. Le Lobby propose la catégorie et l’entraînement. Voir VALIDATION.md pour distinguer tests exécutés et recette visuelle restant à réaliser ; le projet complet reste en cours.

## 26 octobre — suite de l’éditeur et premières récompenses

La capture utilisateur a montré que la première interface demandait des GUID et exposait les types CLR. L’éditeur est remplacé par des sélections nommées, des étapes visibles, des diagnostics et des outils de placement explicites. La réouverture des cinématiques réutilise désormais l’événement et le contenu existants.

Les médailles royales, la progression individuelle et une boutique d’objets empilables sont raccordées à la sauvegarde native. Le serveur décide du versement et des prix ; l’inventaire plein, les achats rejoués et les échecs de sauvegarde sont couverts. Cela remplace la restriction « entraînement sans économie » de la première livraison. Aucun PvP, siège, tournoi ou décor final supplémentaire n’est déclaré implémenté par cette tranche.

## Suite PvP — 26 octobre 2026

Le prototype comprend désormais des invitations et un duel entre deux véritables Player. La simulation unique est protégée par les deux verrous d’entité, acquis sans attente. Le calculateur JoustMatch existant résout les lances ; le Cinematic Director existant pilote les scènes. Les départs et la réservation sont revérifiés à l’acceptation. Le nettoyage périodique couvre aussi deux joueurs déconnectés. Les personnages adverses ne sont jamais disposés comme les anciens acteurs IA temporaires.

Les gardes de déplacement, combat, voyage et changement de monture s’appliquent aux deux côtés. Le nettoyage et la progression distinguent le numéro de côté. Les tests couvrent les deux gagnants, les invitations invalides, les replays et les interruptions. Les sièges, tournois, décor final et intégrations de progression restent ouverts.
