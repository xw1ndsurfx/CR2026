# Royal Guide — Phase 3 : interactions réelles avec les événements

## Dépendances et statut

La PR #249 reprend `main` après les PR #247 et #248. Le serveur, le client et l'éditeur doivent utiliser des versions compatibles. Le client privé Intel HD 3000 reste sur sa branche séparée ; aucun exécutable installé n'est remplacé par cette validation.

## Objectif

Permettre d'enseigner une **vraie interaction** avec un personnage ou un objet de la carte, sans simple écran de texte et sans crédit de progression donné par un clic non vérifié. Un événement de carte peut avoir une apparence de PNJ (graphisme et dialogue) : il reste identifiable par son `EventDescriptor.Id`.

### Nouveau type d'étape

- `TutorialObjective.InteractEvent` : l'étape est complétée automatiquement **uniquement lorsque le moteur serveur active réellement l'événement ciblé**.
- `TutorialHighlightTarget.WorldEvent` : anneau et flèche sur l'événement de carte exact chargé et visible.
- `TutorialStep.TargetEventId` : identifiant persistant du `EventDescriptor`.
- `TutorialStep.MapId` : carte où cet événement doit être activé.

Les IDs des objectifs et cibles antérieurs sont inchangés. Les configurations JSON de phase 1 et 2 restent compatibles. Aucun nouveau paquet de validation client, migration SQL ou jeton d'interaction n'est ajouté.

### Preuve de réussite côté serveur

`Player.RecordTutorialEventInteraction` n'est appelé qu'après que l'activation de l'événement a satisfait les validations moteur et reçu une pile de commandes :

1. `ActionButton` : événement activé par le joueur, proximité contrôlée, pas de commande déjà en cours ;
2. `PlayerBump` : collision de direction validée par le serveur, bon déclencheur et pile inactive ;
3. `PlayerCollide` : collision sur la carte et démarrage de l'événement acceptés.

Le serveur vérifie également que le tutoriel est actif, que le joueur est vivant, que l'ID de l'étape et la révision correspondent, que l'objectif est `InteractEvent` et que les IDs de carte **et d'événement** coïncident. Le paquet générique `TutorialAction.Next` ne valide pas cet objectif (`TutorialReady` retourne faux).

La validation se fait **au déclenchement** de l'événement, pas à la fermeture d'un dialogue ou à l'acceptation d'une quête. Pour une condition de fin plus avancée, utiliser l'objectif `Variable` et mettre à jour une variable par les commandes de l'événement.

## Utilisation : exemple PNJ du village

1. Dans Map Editor, créer un événement à l'emplacement du guide et lui attribuer le graphisme d'un personnage.
2. Régler son déclencheur sur `ActionButton` (interaction classique) et lui donner une page de dialogue/actions valide.
3. Dans **Tutorial Editor → Guide visuel**, choisir la carte, puis **Événement de carte ciblé**. Ouvrir la liste demande la carte au serveur si elle n'est pas encore chargée. Les coordonnées et un identifiant court distinguent les événements homonymes.
4. Appuyer sur **Exiger cette interaction**. Cela transforme l'étape sélectionnée en `WorldEvent` / `InteractEvent` ; pour une étape supplémentaire, utiliser d'abord **Ajouter une étape**. Changer de carte efface la cible précédente.
5. Écrire le titre et la consigne en FR/EN/ES dans les onglets de traduction.
6. Enregistrer, reconnecter un personnage de test et vérifier le cercle et la flèche au-dessus du bon personnage.
7. Lui parler : la progression passe à l'étape suivante après l'activation vérifiée.

Le panneau de prévisualisation illustratif de Tutorial Editor est **cliquable** : les zones du menu, le joueur, la mini-carte, le PNJ/événement ou une zone de terrain permettent de présélectionner une surbrillance. Ce n'est pas une vraie capture de l'éditeur de carte. Si plusieurs personnages portent le même nom, le repère d'événement ne se base **jamais** sur le nom.

## Validations de configuration

Le serveur refuse l'enregistrement si :
- un objectif `InteractEvent` n'a pas de `MapId` ou de `TargetEventId` ;
- un marqueur `WorldEvent` n'a pas de carte ou d'événement ;
- la carte n'existe pas ;
- le descripteur choisi n'est plus présent dans la liste des événements de cette carte, est un événement commun, n'a aucune page ou appartient à une autre carte ;
- aucune page n'utilise un déclencheur `ActionButton`, `PlayerBump` ou `PlayerCollide` pour l'objectif `InteractEvent`.

Le client masque le repère lorsque la carte ou l'événement n'est pas disponible, au lieu de guider vers un mauvais personnage.

## Scénarios d'intégration à exécuter

1. Interagir avec le bon événement dans la bonne carte : l'étape se termine et la suite s'affiche.
2. Cliquer sur le bon événement **trop loin** : aucune progression.
3. Cliquer un autre événement ou être sur une autre carte : aucune progression.
4. Envoyer `TutorialAction.Next` durant `InteractEvent` : aucune progression.
5. Mettre le guide en pause puis activer l'événement : aucune progression.
6. Tenter l'interaction mort ou sur un événement déjà actif : aucune progression.
7. Tester `ActionButton`, `PlayerBump` et `PlayerCollide` séparément.
8. Tester plusieurs événements avec un nom et un graphisme identiques : seul l'ID choisi compte.
9. Tester zoom, redimensionnement et événements absents : aucune surbrillance trompeuse ni blocage du jeu.
10. Tester l'éditeur : sélection de carte, changement de cible, bouton d'étape rapide, aperçu cliquable et persistance JSON.
11. Tester l'enregistrement avec carte erronée / événement absent / événement commun : rejet explicite.
12. Rejouer un ancien tutoriel sans réinitialiser sa progression ; confirmer les autres exercices et cinématiques.
13. Vérifier les traductions FR/EN/ES et l'absence de capture de souris/clavier par les marqueurs.

La recette automatisée et ses limites sont décrites dans `RoyalGuidePhase3-validation.fr.md`. Une validation automatisée ne remplace pas l'essai en jeu avec les événements et les dialogues du serveur de test.
