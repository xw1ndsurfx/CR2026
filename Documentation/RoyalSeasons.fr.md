# Donjons à modificateurs et saisons royales

Développement du 22 novembre 2026, branche `codex/dungeon-modifiers-royal-seasons`, issue de `main` après la PR 278. Les deux fonctions sont désactivées par défaut. Le profil privé Intel HD 3000 reste séparé ; ce travail ne remplace aucun client installé.

## Donjons à modificateurs

Dans l’éditeur de donjons, ouvrir **Configurer les modificateurs** dans les paramètres d’instance. La fenêtre permet de composer jusqu’à trois modificateurs fixes, ou une réserve de douze modificateurs dont un à trois sont sélectionnés chaque semaine. La semaine commence le lundi à 00:00 UTC. Le résultat est déterministe pour un donjon et une semaine et ne dépend pas de l’ordre de la liste.

Chaque modificateur possède un nom et cinq réglages :

| Réglage | Valeurs |
| --- | --- |
| Points de vie des ennemis | 100 à 500 % |
| Dégâts infligés par les ennemis | 100 à 300 % |
| Temps restant | 25 à 100 % du temps normal |
| Vies retirées | 0 à 98, avec au moins une vie conservée |
| EXP de fin de donjon | 100 à 300 % |

Les écarts au réglage normal s’additionnent, avec les mêmes plafonds. Le temps réduit nécessite un donjon déjà limité dans le temps. Les modificateurs ne changent pas les objets distribués ni l’EXP normale des monstres. Les soins ne sont pas multipliés par le bonus de dégâts.

La porte affiche un aperçu des noms, effets, vies, durée et EXP avant l’entrée. Les règles deviennent fixes au lancement effectif de la tentative, y compris si la rotation ou la configuration change ensuite. Le panneau de suivi affiche les noms des modificateurs et leurs effets en infobulle. Les ennemis déjà présents et ceux qui apparaissent ensuite dans l’instance utilisent ces règles ; les familiers et les autres instances ne sont pas concernés. Les multiplicateurs ne se cumulent pas lors d’une reconnexion.

Enregistrer ensuite la configuration générale des donjons : **Appliquer au donjon** ne ferme que la fenêtre secondaire et ne transmet pas encore la configuration au serveur.

## Saisons : parcours gratuit et bonus Premium

Le menu du joueur contient **Saisons royales**. La fenêtre présente les dates UTC, les objectifs, les points et les récompenses par palier, avec un défilement jusqu’au dernier palier. Les récompenses gratuites et Premium se récupèrent séparément. Les personnages, niveaux, inventaires et métiers ne sont jamais remis à zéro.

Un compte sans Premium gagne normalement les points et récupère le parcours gratuit. L’abonnement Premium est vérifié par le serveur au moment de récupérer chaque bonus. Devenir Premium plus tard permet de récupérer les bonus des paliers déjà atteints, tant que la période de récupération n’est pas terminée. L’expiration du Premium ne retire ni les points ni les objets déjà obtenus.

Les objectifs disponibles sont :

- Victoires en donjon, avec ou sans filtre sur un donjon précis.
- Victoires dans un donjon à modificateurs. Elles comptent également pour un objectif général de donjon.
- Récompenses d’expédition navale récupérées, avec ou sans filtre sur l’expédition. Le point de validation est la remise effective de la récompense, pas seulement la fin du combat.
- EXP de métier réellement gagnée : métiers ordinaires, agriculture, pêche, exportation, navigation, artillerie, charpenterie navale et joute. Le filtre permet de viser un métier précis. Les modifications administratives de niveau/EXP ne constituent pas un gain de jeu.

Chaque objectif donne ses points une seule fois, quand sa quantité requise est atteinte. Les compteurs sont plafonnés. Les objectifs ne se réinitialisent pas quotidiennement ou hebdomadairement ; créer une nouvelle saison pour un nouveau parcours. Les gains agricoles sont comptés au moment où les cycles sont réglés par le serveur, y compris pour les cycles réalisés hors connexion.

## Configuration d’une saison

1. Ouvrir **Saisons royales** dans le centre des éditeurs, catégorie économie.
2. Ajouter une saison, définir son nom, sa description et ses dates de début/fin en UTC.
3. Ajouter les objectifs, choisir les activités/cibles, la quantité requise et les points accordés.
4. Ajouter les paliers dans l’ordre croissant de points. Choisir des objets existants et leur quantité pour le parcours gratuit et les bonus Premium ; un emplacement vide n’accorde rien. Au moins un palier doit offrir une récompense gratuite. Le dernier palier doit pouvoir être atteint avec les points des objectifs.
5. Choisir le délai supplémentaire de récupération des récompenses, de 0 à 30 jours. Pendant ce délai, les objectifs ne progressent plus.
6. Activer la saison et le commutateur général, puis **Enregistrer les saisons**. Attendre la confirmation du serveur.

Deux saisons actives ne peuvent pas se chevaucher. La période de récupération d’une saison peut cependant coïncider avec la suivante. Une saison activée et déjà commencée ne peut plus être modifiée ou supprimée, pour préserver ses objectifs, seuils et reçus. Le commutateur général suspend la progression et la récupération sans effacer les données et sans décaler les dates. Préparer les contenus en avance, avant activation.

La sauvegarde de l’éditeur utilise une révision : si un autre éditeur a enregistré entre-temps, rouvrir la configuration plutôt que d’écraser ses changements. Une absence de réponse n’est pas présentée comme une réussite. Les noms et descriptions saisis utilisent le catalogue de traduction du contenu ; les commandes d’interface sont disponibles en anglais, français et espagnol.

## Persistance et déploiement

Configuration serveur : `resources/royal-seasons.json`. Les règles des donjons restent dans leur configuration existante. La progression par personnage et les reçus des deux parcours sont stockés dans le profil JSON existant `Players.Pets`, à côté des autres données de collection ; aucune nouvelle migration SQL n’est nécessaire.

La remise d’un objet saisonnier et son reçu sont enregistrés dans la même sauvegarde du compte. Inventaire plein, échec d’écriture ou clic répété ne consomment pas la récompense. Les transactions de pêche, exportation, agriculture, expédition et métiers navals/joute incluent leur progression saisonnière et la restaurent si la sauvegarde échoue. Les gains ordinaires et victoires en donjon suivent le mécanisme de sauvegarde différée déjà utilisé pour la progression.

Distribuer ensemble des versions compatibles du serveur, du client et de l’éditeur. Les réglages et les récompenses doivent être choisis dans l’éditeur avant activation ; aucun prix, objet ou calendrier du serveur réel n’est modifié par cette branche. Le client Intel reçoit ultérieurement les changements communs par son circuit privé habituel.

## Vérification

Validation locale : 728 tests serveur réussis, dont 56 contrôles ciblés des saisons, donjons et expéditions. Les scénarios d’interface passent dans les neuf combinaisons langue/résolution, et les contrôles de l’éditeur passent dans les trois langues. Compilation Release du serveur, du client et de l’éditeur réussie. Les 93 entrées de traduction du module couvrent EN/FR/ES avec les mêmes paramètres de formatage.

```text
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Release --filter "FullyQualifiedName~Season|FullyQualifiedName~Dungeon|FullyQualifiedName~Expedition"
dotnet run --project Utilities/RoyalSeasonUiTests/RoyalSeasonUiTests.csproj -c Release
dotnet run --project Utilities/InterfaceEditorTests/Intersect.InterfaceEditorTests.csproj -c Release -p:RuntimeIdentifiers=win-x64 -- --seasons-only
```

Les tests d’interface utilisent les vrais contrôles avec rendu logiciel, en trois langues et aux résolutions 640×480, 1280×720 et 1920×1080. Ils ne remplacent pas une recette GPU et réseau sur la copie isolée. Avant activation publique : lancer un donjon modifié à plusieurs, vérifier les dégâts/temps/vies, terminer une expédition, gagner de l’EXP de métier, récupérer chaque parcours avec et sans Premium, reconnecter puis redémarrer le serveur et vérifier les reçus et inventaires.
