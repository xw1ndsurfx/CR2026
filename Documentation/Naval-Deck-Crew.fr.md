# Équipage visible et premier plan du bateau

Le rendu monde sépare les bordures qui cachent les pieds et la couche qui masque les fûts mobiles. Les canons doivent passer devant les bordures dans leur zone de balayage, tandis que la toile et ses contours restent devant eux. Les personnages aux canons éloignés passent devant leur propre canon ; les personnages proches restent derrière les bordures. Les textures de premier plan utilisent les pixels du dessin original.

## Activer le trois-mâts

Dans la définition du navire, sélectionner `Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png`, quatre images et 180 ms par image. Les quatre PNG (base, premier plan, canons, occultation des canons) et le `.png.naval.json` correspondants sont fournis dans `resources/ocean`. Régler la capacité totale à **sept : un capitaine à la barre et six canonniers**. Les dimensions de collision restent un réglage distinct : elles doivent être adaptées et essayées au quai pour ce grand navire. Aucun ancien navire n’est remplacé automatiquement. Le modèle v3 reste disponible avec ses canons fixes.

Le pack compact fait 3072 × 1024, avec `PixelsPerTile = 24`. Le pack maître fait 6144 × 2048, avec `PixelsPerTile = 48` : les deux occupent exactement la même taille dans le monde. Les quatre phases font légèrement flotter le bateau ; elles ne déforment pas la toile.

Ne pas sélectionner la texture `fringe` comme bateau autonome. Elle est chargée via `ForegroundGraphics` dans les métadonnées de la base. Ne pas réutiliser l’ancien petit idle ou un ancien sprite de dégâts pour ce nouveau modèle : les couches et ancrages doivent être préparés ensemble pour chaque état graphique.

## Métadonnées et synchronisation

`ForegroundGraphics` indique une texture Ocean de mêmes dimensions que la base. Chaque image dispose de `CrewAnchors`, dont les coordonnées sont locales au rectangle de l’image : position des pieds, direction du personnage, rôle et index du poste. Les rôles sont Helm, PortCannon, StarboardCannon et Passenger. Les ancrages changent avec l’angle et suivent la même phase de flottement que les deux textures. Un nom de ressource hors du dossier Ocean, des coordonnées invalides et des postes répétés sont refusés.

Le serveur ajoute les occupants des canons aux snapshots des bateaux. Il n’expose ni jeton de réservation ni données privées supplémentaires. Seuls les postes actifs, non expirés, appartenant encore à l’équipage sont envoyés. Le pilote réel reçoit le poste de barre, y compris après un transfert. Les membres sans canon sont placés sur les ancrages passagers disponibles.

Le client réutilise le rendu du personnage déjà reçu : corps, coiffure, apparence, équipement, transformation et règles de visibilité. Il ne peint pas d’avatars dans la texture du navire et n’invente pas d’apparence si une entité manque. La position et la direction de dessin sont temporaires ; les coordonnées réseau et les limites d’interaction sont restaurées même si le dessin échoue. Les montures et poses assises ne sont pas dessinées sur le pont.

Si la couche de premier plan ou les ancrages manquent, les anciens bateaux conservent leur rendu précédent. Les personnages du pont sont affichés dans le monde en 2D. Les navires vus depuis une caméra canon ou cinématique reçoivent aussi leurs canons indépendants et leur premier plan ; leurs personnages ne sont pas encore raccordés à ces caméras. L'illustration du canon personnel en première personne conserve ses poses existantes.

## Canons indépendants

`CannonGraphics` charge une petite texture de 512 × 384 contenant 48 cellules transparentes : six postes pour chacune des huit orientations. `CannonAnchors` définit le pivot de chaque poste, son côté, son index, sa cellule et son passage avant ou après les personnages. Les cellules font 64 pixels avec leur pivot au centre, huit colonnes. Les coordonnées suivent les quatre phases de flottement. Les affûts en bois et les encadrements dorés restent fixes.

`CannonOcclusionGraphics` distingue l'occultation des canons de celle des personnages. Les couleurs des bordures et leurs contours sont retirés de cette couche dans le balayage des fûts ; les pixels de toile et leurs contours sont protégés. Le premier plan habituel reste affiché sous les fûts pour masquer les pieds. Sans cette ressource optionnelle, le client conserve l'ancien ordre de rendu. La même séparation est appliquée aux caméras en perspective.

Les pixels métalliques visibles sont extraits de la base et du premier plan v3 pour éviter un deuxième canon immobile sous le canon animé. Les fûts éloignés dans les diagonales réutilisent le petit fût métallique de profil, tourné et dessiné sous les voiles, les affûts et les cordages. L'ouverture sombre sous le fût mobile est reconstruite ; contrôler ses bords dans le client final. L'animation est une rotation de sprite en 2D autour de la culasse, pas un modèle 3D ni de nouvelles poses peintes à chaque angle.

Le serveur conserve `Yaw` sur la réservation du poste et l'ajoute à `NavalCrewStation` (clé MessagePack 3). Une visée incorrecte, hors de l'arc, non finie, envoyée avec un autre jeton ou sur l'autre bord est refusée. Un poste libéré ou expiré revient visuellement au centre. Les mises à jour de visée sont limitées à une toutes les 150 ms côté client lorsqu'elle change ; une seconde suffit à l'arrêt. Le serveur limite ces renouvellements à 100 ms, séparément des actions, afin qu'une visée ne bloque pas le tir. Les autres joueurs interpolent les angles reçus ; le canonnier local voit immédiatement son angle.

Les sources de découpe et le script Windows sont dans `tools/naval`. Les images générées servent de masques de sélection, les pixels affichés viennent des atlas existants. Pour reconstruire : `pwsh tools/naval/Separate-RoyalCannons.ps1 -MaskDirectory <masques> -OutputDirectory <sortie>`. Le script refuse les cellules vides et les métadonnées dépassant la limite de 64 Kio. Compiler et distribuer ensemble le client et le serveur pour transmettre la nouvelle valeur de visée.

## Déplacement libre sur le pont : préparation

La marche sur le pont n'est pas encore implémentée. Le comparatif hors ligne de `Render-DeckScaleComparison.ps1` montre sept personnages de démonstration à 100 %, 75 % et 50 %, avec six angles de canon différents simultanément. Il conserve le même bateau, les mêmes voiles et les mêmes pieds d'ancrage ; ce n'est pas une capture multijoueur ni une modification de l'échelle en jeu. Les poses des mains devront être recalées si une réduction est retenue.

La suite devra utiliser un repère local au navire pour les déplacements et les collisions, puis une projection du pont propre à chaque vue du sprite. Les positions devront rester attachées au pont pendant les translations, les virages, les phases de flottement et les changements de carte. Un masque de marche distinct des textures définira le bord du pont et les obstacles solides (mâts, affûts, cabine). La transparence des voiles ne doit pas définir les collisions.

Le serveur devra valider les pas dans ce masque, la proximité du poste demandé et la disponibilité du canon avant d'accorder la réservation. Le canon sera libéré en quittant le poste. La capacité totale restera sept ; un joueur qui marche occupera toujours sa place dans l'équipage. L'échelle visuelle du personnage, de ses équipements et de son animation ainsi que l'empreinte de ses pieds sur le pont devront être cohérentes. Ces changements doivent rester propres au pont et préserver l'apparence à terre.

## Vérification

Les tests couvrent les postes du pilote et des canonniers, les passagers, les réservations expirées, la sérialisation réseau, les directions miroir, les coordonnées invalides, la restauration du personnage et le couple d’atlas fourni. Un aperçu hors ligne utilise les véritables feuilles de corps et d’équipement du jeu, avec et sans premier plan. Il ne remplace pas un essai multijoueur dans le client final.

Avant mise en production, vérifier les huit angles à l’arrêt et en mouvement, les changements de carte, l’entrée et la sortie d’équipage, le transfert de barre, les acquisitions/libérations de canons, les cheveux/capes/armes de différentes tailles, les effets de transformation et la visibilité furtive. Vérifier également le pilote occulté par la voile dans la vue de face et les toiles devant les mâts et échelles dans les deux diagonales de face.
