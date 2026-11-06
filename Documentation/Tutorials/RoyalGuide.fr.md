# Royal Guide — tutoriel interactif (première intégration)

## Objectif

Remplacer le grand dialogue centré du tutoriel par un guide contextuel et non bloquant,
tout en conservant le protocole serveur, la progression sauvegardée et les objectifs
existants.

## Fonctionnement

- Carte compacte déplacée automatiquement pour ne pas cacher la cible.
- Barre dorée de progression, titre, instructions, commandes Suivant / Pause,
  rejouer le tutoriel et voir une cinématique si configurée.
- Voile transparent avec zone mise en lumière, halo pulsant et flèche animée.
- Clics et clavier toujours accessibles dans le jeu sous le voile.
- Étapes de combat : les conseils de combat existants du HUD sont conservés,
  l'overlay est désactivé pour ne pas masquer les attaques et déplacements.
- Les actions réelles sur **Inventaire** et **Quêtes** passent automatiquement
  à la prochaine étape quand le serveur autorise déjà `Next`.
- Le guide respecte le déplacement, le repli et le redimensionnement du menu.
- Les anciens tutoriels sauvegardés gardent leur progression et leurs identifiants.
  Les titres intégrés connus obtiennent automatiquement une cible visuelle.

## Tutoriel Editor

Dans le PropertyGrid de chaque étape, les nouvelles propriétés sont :

- `HighlightTarget` : `None`, `Menu`, `Inventory`, `Quests`,
  `Minimap`, `Character`, `WorldMap`, `Hotbar`.
- `HighlightStyle` : `Outline` ou `Circle`.

Ces propriétés sont enregistrées dans le JSON tutoriel existant; aucun changement
de protocole, de base de données ou de migration SQL n'est nécessaire.
Pour le guide initial, les cibles suggérées sont menu, inventaire, quêtes
et mini-carte.

**Limite de cette phase** : l'ouverture des fenêtres Inventaire/Quêtes est
reconnue depuis les boutons du menu; les autres étapes restent validées par les
objectifs serveur existants ou par `Suivant`. Les cibles mondiales (PNJ,
sol, flèche au-dessus d'un objet) demandent une seconde phase avec coordonnées
de monde et sélection de PNJ/événements dans l'éditeur.

## Vérifications manuelles à effectuer dans le client

1. Premier login : progression conservée et guide affiché près du gameplay.
2. Redimensionner la fenêtre : carte et surbrillance restent dans les limites.
3. Menu ouvert/fermé : le halo suit la vraie ligne ou le bouton de déploiement.
4. Inventaire : clic sur la vraie ligne => fenêtre visible => transition serveur.
5. Quêtes : même scénario; vérifier que les étapes non ciblées ne progressent pas.
6. Combat : aucune capture de clavier/souris; les conseils d'endurance restent visibles.
7. Cinématique : halo et guide cachés tant que la cinématique est active.
8. Pause, reprise, reconnexion et relance du tutoriel : aucune régression.
9. Éditeur : changer `HighlightTarget` et `HighlightStyle`, sauvegarder,
   ouvrir une nouvelle session et vérifier leur persistance.
10. FR/EN/ES : titre Guide royal / Royal Guide / Guía real et contenus existants.

Aucune compilation du client .NET ni test de jeu n'a été exécuté dans
l'environnement de création de cette PR.
