# Bateaux Ocean — ressources et essai de navigation

## Ressources intégrées pour le prototype

Le client charge les PNG dans `resources/ocean`, séparément des avatars dans `resources/entities`.
Deux originaux fournis sont inclus, sans modification de leurs pixels :

- `Ship_CorpsRoyaux_002.png` : essai de bateau royal normal.
- `Ship_CorpsRoyaux_005.png` : variante endommagée réservée au futur état de dégâts.

Chaque PNG fait 1448 × 1086 pixels RGBA. Ce ne sont **pas** des grilles uniformes 8 rangées × 4 colonnes. Les groupes sont disposés sur deux blocs de quatre rangées, avec des espacements variables. La planche `003` a même une autre disposition; elle n'est pas utilisée.

Les fichiers voisins `.png.naval.json` indiquent 32 rectangles et leurs ancrages de coque. L'ordre logique est N, NE, E, SE, S, SO, O, NO, avec quatre poses par direction. `PixelsPerTile: 64` conserve une échelle uniforme; sur les tiles de 32 pixels, le facteur est 0,5. Le point d'ancrage est placé à la coque, sous les voiles, et non au centre de l'image. Le cap physique reste continu.

Les rectangles ont été inspectés dans un aperçu animé et vérifiés automatiquement contre les dimensions et les empreintes SHA-256 des originaux. Cela ne certifie pas un alignement artistique parfait : sillages adjacents, fragments d'eau et voiles qui se touchent persistent dans les sources, particulièrement sur les profils endommagés. Les diagonales doivent encore être confrontées au sens de marche réel en jeu; certaines vues arrière se ressemblent beaucoup. Il s'agit de ressources de prototype à revoir, pas d'un atlas final annoncé sans défaut.

Un fichier de métadonnées absent, invalide ou incompatible fait afficher la coque provisoire. Une planche Ocean n'est jamais interprétée arbitrairement comme une grille régulière. Le cache est réinitialisé à la reconnexion; redémarrer le client après changement des PNG.

Les anciennes configurations `royal-ship.png` et `royal-ship-damaged.png` trouvent ces deux planches si aucun PNG explicite de ces noms n'existe déjà. Les nouvelles préparations automatiques utilisent directement `002` et `005`. Les configurations existantes ne sont pas réécrites.

Les autres planches royales, Herbonia et les effets de canon fournis ne sont pas activés par cette phase. Aucun tir, dégât jouable ou IA n'est ajouté. Les neuf configurations de distribution incluent les PNG et leurs métadonnées dans `Client and Editor/resources/ocean`.

## Données Ocean examinées

La copie `gamedata.db` fournie contient 76 maps au total, dont deux dans le dossier `Ocean` :

| Emplacement | Identifiant réel | Dimensions | Liaison |
| --- | --- | --- | --- |
| Ocean ouest | `feda52a4-8404-4a74-a9ba-5e0140243e85` | 32 × 26 | Droite → Ocean est |
| Ocean est | `ae6c7b95-7a8d-4526-b492-db14ec704b93` | 32 × 26 | Gauche → Ocean ouest |

Les liaisons sont réciproques. Aucun événement local, spawn de PNJ ou attribut bloquant n'est enregistré sur ces deux maps. La simulation de terrain, avec les classes de mouvement/empreinte/topologie du moteur, traverse dans les deux sens sur les vingt lignes centrales testées (centres Y=3,5 à 22,5).

**Une côte dessinée sans attribut bloquant n'arrête pas le bateau.** Vérifier le terrain visuellement et marquer les terres/obstacles dans l'éditeur. Le test ne déduit pas l'eau des tiles. Aucun service n'a été lancé contre la base fournie; elle a été ouverte en lecture seule. Aucun essai connecté à deux clients n'est annoncé ici.

## Procédure exacte sur une copie de test

1. Compiler les exécutables de cette branche; conserver les mêmes clés réseau et la même configuration de compilation pour serveur, client et éditeur. Copier les quatre fichiers de `resources/ocean` vers le même dossier du client de test.
2. Utiliser une copie isolée de la base fournie et des ressources du serveur. Conserver l'original et la configuration navale actuelle séparément.
3. Ouvrir l'éditeur de test et enregistrer les deux maps Ocean. Marquer une petite zone de collision pour l'essai d'obstacle, en laissant libre le corridor entre elles.
4. Clic droit sur une map Ocean → **Préparer un navire de test automatiquement**. Avec les attributs de la copie examinée, la recherche de terrain propose le centre `(15.5, 12.5)` et l'embarquement en case `(15, 9)`. Le résultat réel du serveur fait foi si des entités ou des obstacles ont changé.
5. Rejoindre ce point avec les moyens habituels du jeu, puis `/naval`. Vérifier que le personnage est masqué et que le bateau royal est affiché. W accélère, S freine/recul lent, A/D tournent, Espace freine à l'ancre.
6. Depuis la map ouest, prendre un cap vers l'est (D depuis le nord) et franchir le bord droit. Vérifier la continuité de la coque et de la caméra. Revenir par le bord gauche de la map est.
7. Vérifier que les frontières extérieures d'Ocean et la zone bloquée arrêtent **toute** la coque. Le test de simulation de la base originale n'inclut pas cette zone ajoutée manuellement.
8. Revenir presque à l'arrêt au point d'embarquement de départ, puis `/naval` pour débarquer. Avec un second client, vérifier l'observation du navire, le refus d'une deuxième prise de barre et la marche ordinaire de l'autre joueur.
9. Déconnecter/reconnecter le capitaine. Reprendre la barre près de la coque. Les positions navales ne sont toujours pas persistées après redémarrage serveur dans cette phase.

Le fichier `naval-test-Ocean.json` livré séparément propose deux bateaux avec de nouveaux UUID sur les deux maps, pour un **serveur de test neuf uniquement**. Le renommer `naval.json` seulement dans cette copie de test, après vérification de l'absence de configuration à conserver. La commande de l'éditeur reste préférable si des navires existent déjà.

## Vérifications reproductibles

```powershell
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Debug
dotnet test Intersect.Tests.Client/Intersect.Tests.Client.csproj -c Release --filter FullyQualifiedName~NavalSpriteCatalogTests
```

Audit facultatif en lecture seule d'une copie de base (non exécuté automatiquement dans la CI) :

```powershell
$env:NAVAL_AUDIT_DB = 'C:\chemin\copie-test\gamedata.db'
$env:NAVAL_AUDIT_REPORT = 'C:\chemin\Ocean-terrain-audit.json'
dotnet test Intersect.Tests.Server/Intersect.Tests.Server.csproj -c Debug --filter FullyQualifiedName~ImportedOceanTerrainAndTransitions
```

Cet audit ne contacte pas le serveur, ne crée pas de joueur et n'écrit pas dans la base. Le rapport comprend les propositions de placement sur terrain, les attributs et les lignes de traversée; il ne garantit pas un quai dessiné, l'absence d'entités actives ou l'alignement graphique.
