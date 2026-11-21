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
  Sans réglage de surbrillance, ils restent sans cible jusqu'au choix explicite
  dans l'éditeur. Le choix `None` et le style sélectionné sont toujours respectés.

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
reconnue depuis les boutons du menu ou leurs raccourcis clavier; les autres étapes restent validées par les
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

## Reprise de validation du 20 novembre 2026

Branche remise à jour avec `main` après #276, sans les modifications privées
Intel HD 3000. Aucun binaire installé ni serveur en ligne n'est modifié.

Corrections : boutons redistribués et guide redimensionné à la résolution courante,
positionnement limitant le recouvrement de la cible, halo limité à la partie
réellement visible du contrôle dans ses parents, disparition pendant les fenêtres
modales, absence de remontée forcée à chaque image, suppression du halo à la
destruction du guide et nouveaux choix de l'éditeur traduits en EN/FR/ES.
Une action peut être retentée après reprise ou rejeu : le client ne bloque plus
définitivement un identifiant d'étape déjà soumis. Le serveur reste seul maître
de `CanAdvance`, de l'étape courante et de la révision.

Les vérifications automatisées utilisent les véritables contrôles Gwen avec
des métriques de polices synthétiques. Elles ne constituent pas une recette
graphique en jeu. Les dix scénarios manuels ci-dessus restent à exécuter sur
un serveur de test avec un personnage, avant de considérer le parcours publié
comme validé. La PR reste en brouillon pendant cette recette.

Commandes de vérification locale (Release, .NET 8 ; éditeur sous Windows) :

```text
dotnet test Intersect.Tests/Intersect.Tests.csproj -c Release --filter "FullyQualifiedName~TutorialTests|FullyQualifiedName~TutorialCombatLessonsTests"
dotnet run --project Utilities/TutorialUiTests/TutorialUiTests.csproj -c Release
dotnet run --project Utilities/TutorialEditorTests/TutorialEditorTests.csproj -c Release
```

Résultats : les 10 tests tutoriel/combat et les 17 groupes d'interface réussissent.
Les essais d'interface couvrent cinq résolutions (320×480 à 1920×1080) dans les
trois langues, les reprises, les commandes conditionnées par le serveur, le
combat, les fenêtres modales, le recadrage du halo et les deux chemins de
destruction (guide seul ou interface entière). Client, serveur et éditeur
compilent sans erreur ; les avertissements du dépôt restent présents.
Les trois essais de l'éditeur (EN/FR/ES) réussissent également : choix traduits,
modification de l'étape réelle, conservation des identifiants d'énumération et
aller-retour JSON avec les réglages de surbrillance.
