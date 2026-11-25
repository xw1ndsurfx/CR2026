# Courses royales de montures

Implémentation du 25 novembre 2026 regroupée dans la [PR 283](https://github.com/xw1ndsurfx/CR2026/pull/283), depuis `main` après la PR 282 (`a3ff262ed`). La configuration des circuits et la recette multijoueur connectée restent à effectuer avant activation en production. Le client Intel HD 3000 conserve sa branche et son installation séparées.

## Parcours joueur

Ouvrir **Lobby → Courses royales**, ou le bouton **Courses royales** de l'écurie. Choisir un circuit et rejoindre son point d'inscription, à trois cases au plus. **Participer** inscrit le personnage ; **Entraînement solo** lance une invitation individuelle sans prix. Fermer la fenêtre conserve l'inscription. **Quitter la course** retire le personnage.

La file réunit de 2 à 16 cavaliers selon le circuit. Une fois le minimum atteint et la période d'inscription écoulée, chaque cavalier doit confirmer. Une file pleine prépare immédiatement le départ. Un refus, une expiration ou un changement de conditions avant le départ annule cette proposition. Une nouvelle inscription est alors nécessaire.

Après l'accord de tous, les cavaliers arrivent sur une instance privée commune du circuit. Une monture leur est prêtée, sans déverrouiller cette monture dans leur collection. Le compte à rebours commence après les transferts. Le serveur bloque les départs anticipés. Les cavaliers circulent sans se bloquer entre eux ; tous ont la même vitesse, indépendamment de leurs statistiques et de leur monture habituelle. Les déplacements diagonaux sont ramenés à un axe cardinal.

Le panneau de course affiche le temps, le tour, le prochain passage et la durée restante. Un drapeau indique le prochain passage, avec un rappel au bord de l'écran lorsqu'il est hors champ. Les passages doivent être franchis dans l'ordre ; le dernier correspond à la ligne d'arrivée. Le classement utilise uniquement les déplacements acceptés et l'horloge du serveur. Un abandon, une déconnexion ou un dépassement du temps limite ne crée pas de victoire.

Les joueurs reviennent à leur position de départ dès leur arrivée validée, à la fin de la course ou lors d'un abandon. Un gagnant n'attend pas que les autres terminent pour récupérer son prix. La monture possédée précédente est restaurée si elle reste autorisée. **Retour** ferme la participation terminée et permet une nouvelle inscription ; les sessions terminées sont aussi libérées automatiquement après quinze secondes. Dans **Records et prix**, les récompenses sont récupérées explicitement ; un inventaire plein laisse le prix en attente. Les records d'entraînement sont identifiés séparément des performances en compétition. Un changement de tracé, de vitesse, de monture ou de nombre de tours crée une nouvelle version du classement.

## Préparer un circuit dans l'éditeur

Ouvrir **Éditeur des courses royales** dans les éditeurs de contenu ou dans le centre des éditeurs.

1. Créer une carte dédiée au circuit, avec des obstacles définissant le parcours. Ne pas y placer d'apparitions de PNJ ni d'événements de carte. Les cases du parcours doivent être libres ou porter l'attribut **Walkable** ; les téléporteurs et autres attributs ne sont pas franchissables pendant la course.
2. Ajouter un circuit, nommer/décrire l'activité, choisir sa carte et une carte publique pour l'inscription. Définir les coordonnées d'inscription, de départ et la direction de départ.
3. Choisir une monture existante sans cavalier intégré dans ses images. Vérifier ses animations dans l'éditeur des montures ; elles sont réutilisées en course avec l'apparence du joueur.
4. Régler les tours (1–20), les cavaliers (2–16), la vitesse (100–500 millisecondes par case), l'inscription, la confirmation, le compte à rebours et la limite de temps.
5. Saisir de 3 à 64 passages dans l'ordre, avec coordonnées et rayon de 0 à 2 cases. Les boutons permettent de les réordonner. Le dernier doit être centré sur la case de départ. Deux passages consécutifs ne peuvent pas se chevaucher. Le serveur vérifie leur accessibilité depuis le départ.
6. Ajouter les prix par place : un objet empilable et une quantité, avec un délai éventuel entre deux prix du même circuit pour un même personnage. L'entraînement n'accorde jamais ces prix.
7. Dans **Paramètres et médias**, choisir le fond, l'atlas de six icônes et les neuf sons. Activer le circuit et enregistrer. L'éditeur attend l'accusé de réception ; une erreur laisse le brouillon modifiable. Les noms et descriptions sont transmis au catalogue de traduction de contenu existant après enregistrement.

La liste est vide par défaut : aucun circuit ni événement du serveur installé n'est créé ou activé implicitement. Les modifications d'un circuit actif annulent ses propositions et courses en cours. Les réglages sont enregistrés dans `resources/mount-races.json` côté serveur.

## Sauvegarde et récupération

L'origine du personnage est enregistrée avant l'entrée sur le circuit dans son profil persistant existant. Si cet enregistrement échoue, le transfert n'a pas lieu. Une déconnexion retire la participation, nettoie l'appartenance au groupe et conserve le retour à traiter lors de la reconnexion. Les courses et inscriptions en cours ne reprennent pas après un redémarrage serveur.

Les arrivées validées et les prix en attente sont conservés dans `resources/mount-race-results.json`, avec remplacement atomique et vidage du fichier sur disque. La remise d'un prix sauvegarde ensemble l'inventaire et un reçu dans le personnage avant d'acquitter le journal. Un échec d'acquittement ne permet donc pas une deuxième attribution. Une erreur de sauvegarde du personnage restaure l'inventaire et le reçu en mémoire. Les deux fichiers de configuration/résultats sont exclus de Git ; sauvegarder le journal avec les données joueurs.

Les sorts, attaques, objets, téléportations ordinaires et invocations de familiers sont bloqués pendant la course. Une déconnexion ne laisse pas de membre fantôme dans le groupe. Les règles de combat et de déplacement hors course restent celles du jeu.

## GFX et audio

Le [manifeste](Mount-Racing-Assets.json) décrit onze ressources originales :

- `resources/gui/racing_background_v1.png` : panneau royal brun, or et bleu, chevaux et drapeaux de course.
- `resources/gui/racing_icons_v1.png` : atlas transparent 3 × 2, cheval/prêt/sablier puis passage/trophée/abandon.
- `resources/sounds/racing/{open,ready,countdown,start,checkpoint,lap,finish,reward,cancel}_v1.wav` : sons PCM16 mono à 44 100 Hz.

Les images ont été générées avec l'outil OpenAI intégré ; leurs [briefs exacts](Mount-Racing-Art-Prompts.json) sont conservés. Les sons sont synthétisés localement sans échantillon externe. Les builds normaux du client et de l'éditeur incluent ce pack via `Shared/MountRacingAssets.props`.

Les onze ressources ont été ajoutées dans **CURRENT CLIENT & EDITOR** le 25 novembre 2026, puis vérifiées par empreinte SHA-256. Aucun fichier existant n'a été remplacé. Les exécutables installés et le client Intel HD 3000 n'ont pas été modifiés. Il faut des versions compatibles du client, du serveur et de l'éditeur pour utiliser la fonctionnalité.

L'installateur `Utilities/MountRacingAssets/install_assets.py` ne sélectionne que les onze fichiers autorisés par le manifeste. Il contrôle leurs empreintes, propose une simulation par défaut et sauvegarde les éventuelles versions remplacées dans le dossier local de validation.

## Contrôles et recette restante

Les 451 tests de la sélection montures/joutes/matchmaking/naval/donjons passent, dont 16 cas dédiés aux courses, ainsi que les 10 tests de déplacements en cinématique. Les tests des courses couvrent confirmation individuelle, invitations périmées, départ anticipé, vitesse serveur, diagonales/téléportation, ordre des passages, tours/classement, abandon/délai, règles figées, pistes bloquées, protocole, langues, inventaire plein, erreurs d'écriture, double remise, retour après déconnexion et nettoyage du groupe. La vérification de participation accepte les profils non initialisés sans bloquer les autres activités. Un test utilise les collisions serveur réelles avec deux cavaliers et vérifie les restrictions de combat. Les contrôles de retour simulent le transfert et la persistance ; ils ne remplacent pas une connexion à une base serveur et à plusieurs clients.

Les interfaces Gwen sont contrôlées en EN/FR/ES à 640×480, 1280×720 et 1920×1080, avec captures du panneau et du chronomètre. L'éditeur est contrôlé dans les trois langues, notamment les coordonnées, médias, modifications de passage et la confirmation de sauvegarde. L'atlas possède une vraie transparence ; les sons sont vérifiés pour format, durée, marge avant saturation et extrémités silencieuses.

```text
python Utilities/MountRacingAssets/generate_audio.py --check
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Release --filter FullyQualifiedName~MountRace
dotnet run --project Utilities/MountRacingUiTests/MountRacingUiTests.csproj -c Release
dotnet run --project Utilities/InterfaceEditorTests/Intersect.InterfaceEditorTests.csproj -c Release -p:RuntimeIdentifiers=win-x64 -- --mount-racing-only
```

Avant activation : configurer un circuit sur une copie de test, courir avec au moins deux clients de la même révision, vérifier les latences, la synchronisation du départ, le repère en jeu, les animations des montures, les sons, l'abandon puis la reconnexion. Rejouer ensuite un redémarrage avec un prix en attente. Aucun essai multijoueur connecté ni test de charge n'est revendiqué par les contrôles automatisés.
