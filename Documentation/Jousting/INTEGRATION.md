# Prototype PvE — Les Joutes Royales

Le serveur pilote maintenant un joueur et un adversaire temporaire dans deux couloirs réels de la carte partagée. La charge arrive au milieu de la lice à l’heure d’impact, puis les chevaux poursuivent leur course et repartent dans l’autre sens au passage suivant. Les tests serveur exécutent les trois passes avec les entités et collisions natives. Une recette visuelle avec un client connecté reste nécessaire avant de déclarer le prototype entièrement validé en jeu.

## Installation dans une copie de test

1. Copier les ressources locales du paquet dans les dossiers `resources/images/jousting` et `resources/tilesets` du client et de l’éditeur. Les sources utilisateur et leurs dérivés restent locaux. La grille du décor est de 16x16 pixels ; le code ne modifie pas les cartes de production.
2. Ajouter William dans le Mount Editor, à partir de `resources/mounts-william.example.json`, sans remplacer les autres montures. Cocher **Le sprite comprend déjà le cavalier**. Cellules 128x128, quatre directions, idle 4 frames, start/loop/end 6 frames. Ces cellules incluent une marge transparente et ne sont pas des tuiles de terrain.
3. Ouvrir **Mini-Games → Jousting Editor** sur une carte de test. Créer une arène, renseigner la monture William, puis placer le départ par clic gauche, l’arrivée par clic droit et le couloir adverse par Maj-clic. La lice horizontale doit mesurer un nombre pair de pas, entre 4 et 64. Les deux couloirs doivent rester dans la carte et sans attribut de carte ni entité bloquante.
4. Dans la liste des séquences, choisir introduction, charge, impact, désarçonnement ou victoire, puis **Modifier la cinématique de joute**. Le Cinematic Director natif s’ouvre sur une source éditable. Sa sauvegarde crée un Event de scène natif sur la carte et renseigne son identifiant dans l’arène. Sauvegarder aussi l’arène côté serveur. Les common Events existants restent acceptés pour les anciens raccordements.
5. Les sources de charge/impact utilisent le nouveau réglage de scène `AllowPlayerControl` : la caméra accompagne les chevaux sans les immobiliser. La joute conserve sa propre interdiction serveur des déplacements libres. L’introduction peut tenir le joueur ; le compte à rebours attend sa fin. L’annulation coupe uniquement les Events que la session a lancés. Les scènes ordinaires conservent leur verrouillage historique par défaut.
6. Activer l’arène et se placer à son départ. Ouvrir Personnage → Joutes Royales, ou choisir **Joutes Royales** dans le Lobby des aventures puis **S’entraîner contre l’IA**. Choisir une difficulté et lancer l’entraînement.

Pendant la course, le HUD compact laisse voir la carte. La fenêtre 2,5D s’ouvre pour la visée. Les contours indiquent les zones du plastron et du bouclier ; le réticule blanc montre la position autorisée par le serveur. Les zones sont réglables et correspondent par défaut aux pixels du sprite frontal fourni. Casque et cheval ne rapportent ni score ni bonus de lance. Les corrections de visée successives ne peuvent pas fabriquer artificiellement de la stabilité.

## Lobby

L’activité **Joutes Royales** figure dans les filtres, la création des groupes et les ouvertures de Lobby par Event. Les identifiants réseau des activités existantes sont conservés. Le bouton d’entraînement utilise le même parcours PvE que l’entrée du personnage ; il ne téléporte pas directement dans un duel. Le rendez-vous avec l’hôte respecte les règles de voyage existantes. Une joute active verrouille temporairement son annonce et bloque le voyage ; l’annonce rouvre en fin de duel.

## Sécurité et nettoyage

L’arène est réservée côté serveur. Les deux coups sont résolus ensemble sur l’horloge serveur. Le client ne transmet ni score ni heure d’impact. Les déplacements traversent les collisions natives et leur durée est transmise dans les paquets de mouvement existants. Déconnexion, changement de carte/instance, mort, obstacle ou suspension prolongée du serveur annulent la session et retirent l’acteur temporaire. La monture précédente n’est restaurée que si elle est encore autorisée. Aucun loot, XP ni compte de PNJ persistant n’est créé pour l’adversaire.

## Recette restante

- Validation graphique et parcours souris complet sur une base de jeu de test avec client connecté.
- Duels PvP, invitations et deux clients synchronisés.
- Sièges réels, animations assises, réservations, caméras spectateur et foule.
- Terrain/transitions et tileset final complet ; retouches artistiques et animations supplémentaires. Les premiers 16 objets ne constituent pas le décor final complet.
- Contenu royal final : roi, Gisabelle, héraut, mouvements et cérémonies éprouvés en jeu. Les cinq sources cinématiques sont des bases éditables.
- Récompenses idempotentes, statistiques, quêtes, achievements, classements et tournois.

Le prototype est un entraînement sans économie. L’arène n’est pas activée automatiquement sur les cartes existantes. La checklist du chapitre 15 de `Conception-v2.txt` reste la recette du projet complet.

## Vérification reproductible

`scripts/Verify-Jousting.ps1` compile réseau/client/serveur/éditeur et exécute les tests de règles, de monde, de client et de compilation cinématique. `scripts/Verify-JoustAssets.py <paquet-local>` contrôle le découpage et l’alpha des fichiers livrés. Les suites `Utilities/PokerUiTests` et `Utilities/PokerInventoryTests` couvrent également le Lobby.
