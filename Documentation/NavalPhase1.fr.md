# Corps Royaux — prototype naval, phase 1

## Point de départ et inspection

Base inspectée : `main`, commit `7fec8244b` (PR #149 fusionnée). Aucune PR ouverte au début du travail. La branche `feature/admurin-gui-2026-10-12` reste intacte; ses modifications déjà fusionnées sont conservées via `main`. Aucun changement dans les sous-modules, les bases de données du jeu ou les ressources existantes.

Constats dans le code réel :

| Domaine | Source existante | Adaptation |
| --- | --- | --- |
| Arborescence de l'éditeur | `MapList.List`, `MapListFolder.Children`, `MapListMap.MapId` | Découverte récursive des dossiers nommés `Ocean`, insensible à la casse. Le nom d'une map seul ne suffit pas. |
| Topologie | `MapController.Left/Right/Up/Down`, dimensions de `Options.Instance.Map` | Traversée des liens réciproques existants; conservation du dépassement fractionnaire. Refus des voisins absents ou hors Ocean. |
| Entités | `Entity.X/Y` entiers serveur, octets côté client | État naval indépendant en doubles, position de l'avatar utilisée comme ancrage de chargement. |
| Simulation | `Player.Update(long timeMs)`, verrou `EntityLock` | Intégration du navire sous autorité serveur, sans changer la marche normale des autres joueurs. |
| Réseau | `IntersectPacket`, MessagePack, découverte automatique par `PacketTypeRegistry` | `ShipInputPacket` sans coordonnées et snapshots dédiés. Recompiler ensemble client, serveur et éditeur : l'ajout de types modifie le registre de paquets. |
| Collisions | Attributs, `GetCachedEntities`, événements globaux et `Player.TryGetBlockerOnTile` | Empreinte complète de coque sur les cartes voisines; consultation des obstacles et événements existants. |
| Transitions | `Player.Warp` | Appelé uniquement au changement réel de map, à l'embarquement et au débarquement au quai configuré. Le mouvement courant reste continu dans `ShipState`. |
| Ressources | `MonoContentManager.LoadTextureGroup("entities", ...)` | Fichiers dans `resources/entities`, chargés comme textures d'entités avec grille navale indépendante des sprites humains. |
| Rendu | Ordre des rangées dans `Graphics`, puis couches fringe | Navires à la rangée de leur centre; caméra interpolée indépendante du sprite humain. |

## Fonctionnement livré

Le serveur utilise une configuration opt-in `resources/naval.json`. L'éditeur peut maintenant la créer, y ajouter un navire et activer la navigation immédiatement, sans copie de fichier ni redémarrage. Jusqu'à 64 navires, chacun avec un identifiant stable, une coque et des paramètres propres. L'état en mémoire contient capitaine, map, position précise, cap continu, vitesse, accélération par manette progressive et état de dégâts réservé. Les composantes de vélocité sont dérivées du cap et de la vitesse.

`/naval` prend la barre d'un navire libre à proximité du quai. Après une déconnexion, la même commande permet de reprendre un navire libre lorsqu'on se trouve près de son centre. Une seule acquisition à la fois est possible, sous verrou. Quitter la barre exige un navire presque arrêté, le quai configuré sur la même map à moins de cinq cases, une case de sortie libre et hors coque. Si la coque couvre le point d'embarquement, une case libre voisine du quai est recherchée au lieu de bloquer systématiquement le débarquement (voir `NavalOceanSprites.fr.md`).

W augmente progressivement la poussée; S réduit la poussée, freine, puis autorise un recul plafonné. A/D règlent la rotation, limitée en degrés/seconde. Espace applique le frein d'ancre. Les touches sont neutralisées quand l'interface ou un événement occupe le joueur. La manette revient progressivement au neutre lorsqu'on relâche les touches, avec inertie et frottement. Pas de touche F supplémentaire, pour éviter les raccourcis existants.

Le client envoie au plus 20 entrées/seconde; le serveur rejette les entrées hors plage, une mauvaise identité de navire/capitaine et les paquets acceptés à moins de 25 ms d'intervalle. Après 500 ms sans entrée acceptée, ou pendant une interaction bloquante, il freine. Les entrées n'apportent ni position, ni vitesse, ni delta-time client. Les paquets de marche, attaque, blocage, orientation, utilisation de sort/objet et activation d'événement sont refusés pendant le pilotage. Une route de marche ne peut pas déplacer l'avatar capitaine.

La simulation se subdivise en pas de déplacement d'au plus environ 1/8 de case. Un retard serveur supérieur à 100 ms est plafonné, sans rattrapage brutal. Les snapshots complets des navires des maps voisines arrivent au plus à 10 Hz, avec interpolation client de 100 ms et interpolation angulaire par le chemin le plus court. L'avatar et ses indicateurs sont masqués pendant le pilotage. Le chargement des maps conserve l'infrastructure du joueur existant.

Déconnexion, mort, destruction de l'entité et téléportation externe libèrent la barre et arrêtent le navire. Un déplacement externe ne transporte pas le navire. Retirer une map d'Ocean ou la supprimer libère le capitaine au prochain tick; une map voisine non chargée est traitée comme bloquée. Les ajouts depuis l'éditeur conservent les capitaines et positions des bateaux déjà en mémoire. Les positions initiales sont relues au redémarrage; pas de sauvegarde persistante des déplacements des bateaux dans cette V1.

## Préparer un test local sur Ocean

1. Utiliser une copie locale des ressources et bases du jeu. Le dépôt contient le moteur mais pas les maps/bases Ocean de Corps Royaux. Aucun test réel de navigation sur ces maps n'a pu être effectué ici.
2. Initialiser les sous-modules : `git submodule update --init --recursive`.
3. Construire les trois exécutables avec la même configuration et les mêmes clés réseau :

   ```powershell
   dotnet build Intersect.Server/Intersect.Server.csproj -c Debug
   dotnet build Intersect.Client/Intersect.Client.csproj -c Debug
   dotnet build Intersect.Editor/Intersect.Editor.csproj -c Debug
   ```

4. Dans l'arborescence de l'éditeur, placer deux maps voisines sous `Ocean` ou ses sous-dossiers. Utiliser les liaisons normales des maps et enregistrer les cartes.
5. Faire un clic droit sur une **map** dans la liste, puis choisir **Préparer un navire de test automatiquement**. Le serveur récupère le véritable `MapId`, génère l'ID du bateau et cherche une zone libre pour toute la coque, près du centre de la carte. Il réserve aussi un point d'embarquement libre hors coque, à moins de cinq cases. Rien n'est à saisir dans le JSON.
6. Lire le message de résultat : il indique la position du bateau et le point d'embarquement. La configuration est créée/enregistrée et `Enabled` est activé immédiatement. Un second clic conserve le navire déjà configuré sur cette map et rappelle son point d'embarquement : il ne duplique ni ne replace le bateau. Pour préparer un autre bateau automatiquement, choisir une autre map Ocean.
7. Le placement consulte les collisions enregistrées, les événements, les points fixes de spawn de PNJ, les entités actives et les autres coques/points d'embarquement. Si la place manque, la map est hors Ocean ou le fichier existant est invalide, il refuse sans écraser les données. Les champs de configuration inconnus sont conservés. Un point d'embarquement est une case logique : l'outil ne dessine pas un quai et ne distingue pas l'eau de la terre à partir des images. Marquer les obstacles/terres infranchissables avec les attributs appropriés et vérifier visuellement le point retenu. Les frontières à traverser doivent rester libres pour toute la coque.
8. Démarrer un client compilé avec la même version que le serveur et l'éditeur, puis rejoindre le point d'embarquement indiqué avec les moyens existants du jeu/éditeur. Aucun redémarrage serveur n'est nécessaire après l'action automatique. Le prototype n'ajoute aucune téléportation d'administration.
9. Envoyer `/naval` dans le chat. Vérifier le changement d'avatar vers le navire, puis W, relâchement, A/D, S jusqu'au recul et Espace. Revenir presque à l'arrêt au quai puis `/naval` pour débarquer.
10. Approcher une côte bloquée, un événement non passable, un autre bateau et une frontière vers une map hors Ocean : le centre et toute la coque doivent rester en zone valide. Traverser une frontière Ocean à Ocean dans les deux sens et vérifier l'absence de saut visuel.
11. Avec un second client : vérifier le bateau du premier capitaine, l'absence de double acquisition, l'indépendance de la marche du second joueur et le pilotage d'un deuxième bateau avec un autre `Id`. Déconnecter/reconnecter le capitaine, reprendre avec `/naval`, puis vérifier mort, téléportation par événement et rechargement d'une map sur la copie de test.
12. Tester client, serveur et Game Editor avec le naval désactivé; vérifier marche, combat, événements et transitions ordinaires. Vérifier les messages en anglais, français et espagnol.

La configuration absente/invalide laisse le naval désactivé. Si la barre est indisponible malgré une configuration valide, vérifier instance Overworld, Z=0, absence de mort/échange/boutique/route imposée, dossier Ocean, place libre pour toute la coque et positions des autres navires. Les événements et le terrain doivent être vérifiés dans les maps réelles avant utilisation.

### Configuration manuelle facultative

Pour un placement précis ou plusieurs bateaux sur la même map, l'ancienne méthode reste disponible : copier `resources/naval.example.json` vers `resources/naval.json` dans le répertoire de lancement du serveur, renseigner le vrai `MapId` et un `Id` unique, puis mettre `Enabled` à `true`. L'exemple place le centre en `(10.5,10.5)` et le point d'embarquement en case `(14,10)`; ces valeurs doivent être adaptées à la map réelle. Les coordonnées du centre sont précises, celles du point d'embarquement sont des indices de cases. Un redémarrage reste nécessaire après une modification manuelle des paramètres/positions de navires déjà chargés. L'action automatique de l'éditeur est réservée aux connexions Game Editor authentifiées et n'est jamais déclenchée par une simple ouverture de carte.

## Sprites Ocean fournis

Les ressources réelles fournies le 19 octobre sont maintenant prises en charge dans `resources/ocean`, avec des rectangles et ancrages explicites. Voir [NavalOceanSprites.fr.md](NavalOceanSprites.fr.md) pour les planches choisies, les limites artistiques constatées et les deux maps Ocean auditées. Les anciennes configurations sont conservées.

## Ancien format régulier facultatif

Le format ci-dessous reste disponible pour des PNG placés dans `resources/entities`. Il ne décrit pas les planches fournies dans `resources/ocean`, qui utilisent leurs métadonnées explicites et ne doivent pas être découpées avec cette grille.

Fournir des PNG transparents aux chemins client :

- `resources/entities/royal-ship.png`
- `resources/entities/royal-ship-damaged.png`

Chaque image doit contenir **8 rangées × 4 colonnes** par défaut. Ordre des rangées : **N, NE, E, SE, S, SO, O, NO**. Chaque rangée pointe réellement la proue dans ce sens. Chaque colonne représente une pose animée, avec même taille de cellule et même centre de coque dans toutes les cellules. L'image totale doit être divisible par `Frames` horizontalement et par 8 verticalement. Les cellules sont affichées à leur taille pixel native, sans rotation arbitraire du bitmap; choisir leur taille pour correspondre à la coque en cases et à la taille des tiles du jeu. Le centre de la cellule correspond exactement au centre physique de la coque. Pas de marge ou de séparateur entre les cellules.

`Frames` et `FrameMilliseconds` sont configurables; les fichiers de graphisme normal/endommagé le sont également. Le moteur vérifie les dimensions divisibles, **pas l'alignement artistique de la proue ou de la coque** : vérifier ces points visuellement avant de remplacer le bateau de test. Fournir les planches sources et leur grille permettra cet audit. Une texture manquante ou une grille non divisible affiche une coque de test bleu/or dessinée par le code en gros pixels, avec huit orientations et sillage animé. Ce dessin n'est pas une ressource artistique finale. Les frames de voiles et d'eau doivent être intégrées dans les cellules de la planche fournie si elles doivent s'animer ensemble.

## Collisions et limites assumées

- Empreinte conservatrice : disque de rayon `sqrt(Width² + Length²)/2`, contenant toute la coque rectangulaire à tous les angles. Il évite la traversée par les coins, mais peut refuser un chenal visuellement assez large. Les autres coques sont également comparées par les cases couvertes. Une empreinte orientée plus précise peut remplacer `ShipFootprint` ultérieurement.
- Attributs bloqués en V1 : Blocked, animation bloquante, Resource, Warp, Slide, GrappleStone, tout ZDimension. Le naval reste au niveau Z=0. Les ressources non bloquantes sont traitées conservativement si leur attribut est présent.
- Événements globaux et pages locales pertinentes pour le capitaine sont consultés. Aucun déclencheur de contact naval dédié n'est ajouté; les événements et changements de map existants restent ceux de l'ancrage joueur.
- Un bateau est une entité navale réseau distincte, rendue par `NavalWorld`, et non un PNJ/humain transformé. Il n'est pas encore une cible de combat Intersect et n'est pas enregistré comme `Entity` standard dans les listes de PNJ. La marche des autres entités n'est pas modifiée pour leur faire contourner une coque; le bateau, lui, détecte ces entités et s'arrête. À traiter avant combat/abordage.
- Le capitaine conserve ses statistiques et vulnérabilités d'avatar dans le moteur; ce prototype de navigation ne remplace pas le combat par des dégâts de coque.
- Pas de persistance des bateaux, propriété durable, équipage sur pont, tirs, canons, IA navale ou vue canon immersive dans cette phase. Les navires sans capitaine restent ancrés. Un navire dont la map disparaît n'est pas téléporté ailleurs.
- Le bateau est rendu avant les couches fringe : vérifier la disposition des ponts/quais des maps de production. L'interpolation s'arrête sur le dernier état connu; après 1,5 s sans snapshot, les données visuelles expirent. Pas de prédiction client, ni compensation avancée de latence.

## Points de raccord pour les phases suivantes

| Extension | Raccord prévu |
| --- | --- |
| `ShipCrewSession` | Associer les futurs membres au `ShipState.Id` stable; conserver l'autorité unique de `CaptainId`. Ajouter un état serveur de session, puis une map de pont instanciée reliée à la position Ocean du bateau. Le `MapInstanceId` actuel du capitaine reste un détail de l'ancrage Overworld. |
| `ShipCannonStation` | Stations indexées par bateau et emplacement local sur la coque; validation serveur de l'appartenance à la session, du tireur et du cooldown. Ne pas faire passer le contrôle de station par `ShipInputPacket`. |
| Projectiles et dégâts | Calculer l'origine et la direction depuis `ShipState.X/Y/Heading` et `VelocityX/Y`, via la même topologie Ocean. Brancher les dégâts dans `DamageState`, puis diffuser un snapshot/paquet dédié. `DamagedGraphics` est déjà prévu. |
| IA Herbonia | Ajouter un fournisseur d'inputs serveur utilisant `ShipMovement.Step` et les mêmes collisions, puis une définition avec des graphismes rouge/noir vérifiés. |
| Vue canon immersive | Consommer les snapshots géographiques réels, le cap et les stations; générer la projection pixel art à partir de cet état. Garder le monde Ocean et les tirs autoritaires côté serveur. |
| Sauvegarde et ownership durable | Persister définitions, positions valides, instance logique et propriétaire, puis ajouter migration et reprise testées avant activation permanente. Aucun champ naval temporaire n'est enregistré dans la table Players par cette phase. |

## Vérification automatisée

```powershell
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Debug --filter FullyQualifiedName~NavalTests
```

La suite couvre physique, plafonds, marche arrière, ancre, rotation continue, stabilité selon le tick, pauses, coque contre mur, ownership et cadence des inputs, absence d'input, plusieurs bateaux, découverte Ocean récursive, frontières réciproques, conservation fractionnaire, blocages d'attributs, sérialisation réseau et validation des définitions. Le placement automatique ajoute : empreinte et point d'embarquement, zones étroites/bloquées, réservations, création/activation, idempotence après réouverture, doubles clics concurrents, limite de navires, données invalides et conservation des champs inconnus/modifications concurrentes, protection des paquets éditeur et traductions en trois langues. Le menu et son rendu WinForms sont vérifiés par `Utilities/InterfaceEditorTests` (option `--naval-only`). Ces tests ne remplacent pas la procédure à deux clients sur les bases et cartes réelles.
