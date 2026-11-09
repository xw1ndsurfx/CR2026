# Équipage visible et premier plan du bateau

## Navires demandés et travaux à venir

Demandes du 9 novembre 2026, à réaliser un modèle après l'autre et à vérifier dans les huit orientations. Chaque nouveau modèle doit avoir quatre états : navigation, idle à voiles repliées, endommagé et naufrage. Prévoir des couches de premier plan distinctes, des canons indépendants et des ancrages adaptés à l'équipage à 75 %. Les éléments ci-dessous constituent le travail demandé ; leur présence dans cette liste ne signifie pas qu'ils sont déjà livrés.

Apparence de base demandée ensuite : chaque type de coque doit avoir un modèle « aventurier » sobre pour les joueurs, avec bois naturel, voiles écrues et aucun emblème de faction. Les apparences Corps Royaux, Herbonia, Majestya, pirate, squelette et fantôme restent distinctes. Le nouveau trois-mâts joueur par défaut et le premier choix des modèles utilisent l'aventurier. Les modèles et identifiants déjà publiés ne sont pas remplacés silencieusement : le bouton « Changer l’apparence du navire » permet d'appliquer les quatre états à une offre existante en conservant son identifiant, ses conditions d'accès et les flottes qui la possèdent. Le futur Ship of the Line devra également avoir sa base aventurier.

- Trois-mâts Corps Royaux : idle à voiles repliées, état endommagé et naufrage v5 assemblés ; vérification du raccord des états dans le client final encore nécessaire.
- Herbonia : quatre états rouges assemblés dans les huit directions ; choix des NPC du capitaine et de chacun des six canons dans l'éditeur. Les deux anciens fichiers Herbonia standard sont migrés une seule fois, sans changer les positions, les caractéristiques physiques ni les graphismes personnalisés.
- Squelette : quatre états assemblés dans les huit directions, avec coque sombre, ornements osseux, pavillon à tête de mort et voiles usées ; contrôle visuel dans le client final encore nécessaire.
- Fantôme : quatre états translucides assemblés dans les huit directions ; pixels répartis dans des couches disjointes pour ne pas multiplier l'opacité, canons teintés assortis. Vérification des joueurs et de la lisibilité dans le client final encore nécessaire.
- Pirate : quatre états assemblés dans les huit directions, voiles noires et pavillon noir à tête de mort.
- Majestya : quatre états assemblés dans les huit directions, coque et voiles vertes, drapeau assorti.
- Sloop aventurier et Corps Royaux : quatre états chacun ; coque courte distincte, un mât, deux canons joueurs au total, un de chaque côté, plus le capitaine. Pont, barre et canons recalibrés dans les huit directions.
- Ship of the Line : ajouté à la liste des futurs navires ; grand vaisseau de ligne distinct des modèles précédents.
- Radeau aventurier : quatre états assemblés, un capitaine au timon et un canon joueur à l’avant ; pas de mât ni de voile. L’état idle conserve le même radeau et ses quatre phases de flottement.
- Bateau de pêche aventurier : quatre états assemblés, filets et repose-cannes sans artillerie. À l’arrêt, les six postes ouvrent une zone choisie dans l’éditeur de pêche ; validation de l’immobilité côté serveur.

Les variantes Herbonia, squelette, fantôme et pirate doivent aussi pouvoir appartenir aux joueurs. Prévoir leurs entrées de catalogue et leurs conditions d'obtention configurables : achat à la boutique de bateaux, premium, quête terminée et attribution/déblocage par commande d'événement. Les permissions, paiements et sauvegardes restent validés par le serveur. L'abordage doit utiliser les ponts des bateaux, avec leurs collisions et couches de premier plan, en conservant le combat, les retours et les récompenses existants.

L'éditeur de patrouille ennemie doit proposer une case « Peut aborder les navires des joueurs », décochée par défaut, pour autoriser explicitement les abordages initiés par les ennemis.

Réparations physiques : afficher des trous sur le pont selon les dégâts du navire ; interaction à proximité pour lancer le mini-jeu de réparation existant, synchronisation des réparations entre joueurs et disparition des trous réparés.

Animations de dégâts : zones locales configurables dans l'éditeur naval, chacune avec une animation existante choisie dans l'éditeur d'animations (par exemple de petites flammes), apparition selon les dégâts et extinction après réparation. Maintenir ces zones alignées dans les huit directions et pour les quatre états graphiques.

Le rendu monde sépare les bordures qui cachent les pieds et la couche qui masque les fûts mobiles. Les canons doivent passer devant les bordures dans leur zone de balayage, tandis que la toile et ses contours restent devant eux. Les personnages aux canons éloignés passent devant leur propre canon ; les personnages proches restent derrière les bordures. Les textures de premier plan utilisent les pixels du dessin original.

Le catalogue livré compte **onze modèles et quarante-quatre états** : quatre bases aventurier, six trois-mâts de faction et le sloop Corps Royaux. Les atlas et couches sont contrôlés dans les huit directions. Les aperçus hors ligne utilisent les vrais personnages du jeu ; une validation dans le client graphique multijoueur reste nécessaire avant déploiement.

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

## Déplacement libre à 75 %

Le modèle v4 active le pont praticable. Les personnages et leurs couches d'apparence sont dessinés à **75 %**, autour des pieds ; leurs animations de marche utilisent les feuilles du personnage déjà reçu. Le contexte de dessin est restauré après chaque personnage, même en cas d'erreur. La taille et le déplacement à terre restent ceux du moteur habituel. Les onze modèles enregistrés utilisent leur profil de pont : trois mâts et six postes, sloop à un mât et deux postes, radeau sans mât et canon avant. Les anciens graphismes personnalisés gardent leurs postes fixes.

Les membres embarqués apparaissent sur une place libre du pont au lieu d'être automatiquement installés à un canon. **WASD ou les flèches** déplacent le personnage dans les directions de l'écran. **E** utilise un canon libre à proximité ou une barre vacante. À un canon, E ou la fermeture de la vue canon libère le poste. Les boutons de changement de bord de cette vue ne permettent pas de réserver à distance un autre canon. Les instructions sont traduites en anglais, français et espagnol par le catalogue naval et utilisent la préférence de langue du client.

À la barre, WASD conserve les commandes de navigation et E permet de rester sur le pont en quittant la barre. Le navire continue sur son erre, avec sa traînée normale, tant qu'aucun autre pilote ne reprend la barre. Un membre du groupe peut reprendre une barre vacante en s'en approchant ; les permissions de gestion du capitaine restent applicables. Reprendre la barre ne réinitialise pas les cinq autres réservations de canon. Si un transfert place un nouveau pilote ou canonnier sur les pieds d'un joueur qui marchait, le joueur libre est déplacé sur un emplacement adjacent pour éviter de rester bloqué dans un autre personnage.

`ShipDeck` définit le masque physique partagé : bords du pont, trois mâts, six affûts, tonneaux et gouvernail. Les pieds ont un rayon de 0,12 unité du pont, adapté à l'échelle de 75 %. Les déplacements sont balayés par petits pas, glissent le long des obstacles et tiennent compte des autres joueurs. Le client envoie uniquement une direction ; le serveur calcule la position, limite le temps de mouvement accepté et valide l'appartenance à l'équipage, la carte, l'état du joueur et la proximité du poste. Un joueur ne transmet pas sa position souhaitée et ne peut pas accélérer en répétant des paquets. Les réservations et les rechargements restent sous l'autorité du serveur.

Les coordonnées du pont ne sont pas celles de la carte. Les translations, virages et transitions de carte du navire ne modifient pas ces coordonnées locales. `ShipDeckProjection` les projette sur les huit vues dessinées, avec des repères propres à chaque image et à chaque phase de flottement : `DeckMasts`, `DeckPort`, `DeckStarboard`, `DeckBow` et l'ancrage de barre. Le masque de collision est indépendant de la transparence des voiles ; on peut marcher sous une toile qui masque correctement le personnage. Les réglages se trouvent dans `ShipDeck.cs` et `tools/naval/deck-calibration.json`. `Apply-DeckCalibration.ps1` réapplique les repères après une reconstruction des canons. Le JSON doit rester minifié et inférieur à 64 Kio.

Ce modèle utilise **sept places au total**. Le groupe et le salon naval peuvent atteindre sept membres même lorsque la limite normale des groupes est cinq ; les groupes sans ce navire conservent leur limite configurée. Les cartes de groupe disposent d'une zone de défilement, et le HUD peut afficher les sept membres. La capacité effective est envoyée par le serveur. L'embarquement, les groupes autorisés, les quais et la persistance du passage utilisent les règles existantes des navires personnels.

Le protocole ajoute `DeckInputPacket`, puis les clés 36 à 38 de `ShipSnapshot` pour les membres du pont, son activation et une barre vacante. La clé 1 de `PartyPacket` et la clé 12 de `NavalSnapshotPacket` transmettent la capacité du groupe. Les anciennes données gardent leurs valeurs par défaut. Compiler et distribuer ensemble le client et le serveur ; les PNG et JSON v4/v5 sont copiés dans les sorties du client et de l'éditeur via `Shared/NavalDeckAssets.props`. Les modèles enregistrés sélectionnent leurs états assortis : idle sous 0,05 de vitesse, apparence endommagée lorsque le serveur indique des dégâts, naufrage en quatre phases de 500 ms jusqu'à une image transparente. La marche est désactivée pendant le naufrage.

## Équipage ennemi et abordage

Les patrouilles disposent de sept sélecteurs de NPC existants : capitaine, trois canonniers bâbord et trois tribord. Le serveur transmet leurs sprites, rôles et visées, sans réservations de joueurs. Les personnages passent dans les mêmes couches que l'équipage joueur. Dans les abordages automatiques, les apparences choisies sont clonées en définitions temporaires ; les statistiques de combat conservent l'équilibrage naval selon le niveau de la patrouille. Les NPC d'origine ne sont pas modifiés.

La case « Peut aborder les navires des joueurs » autorise un ennemi à engager un abordage lorsque la coque du joueur passe sous le seuil configuré, à portée et hors des zones de sécurité des quais. Les vérifications sont répétées au démarrage. La création de carte et les verrous des joueurs restent hors du verrou global naval.

Pour deux modèles compatibles, l'abordage automatique crée une scène de combat isolée présentant les vrais navires à voiles repliées, leurs ponts et une passerelle. Le masque partagé est projeté sur les cases de la carte pour les bords, mâts et affûts ; les couches de premier plan passent au-dessus des personnages à 75 %. Les navires du monde sont arrêtés pendant la rencontre. Le système existant conserve le combat des NPC, la validation du groupe, les récompenses, l'échec et le retour au navire. Les donjons d'abordage explicitement configurés et les anciens modèles gardent leur chemin existant.

## Dégâts et droits sur les modèles

Le bouton de configuration des zones de dégâts permet de régler leur position locale, le seuil de coque, une animation existante et le type de réparation. Une zone réparable affiche un trou et peut être utilisée à proximité avec E. Une seule réparation réserve cette zone à la fois ; le serveur contrôle le poste et annule une réparation devenue inutile. Les trous et animations disparaissent lorsque la coque dépasse leur seuil. Les zones par défaut peuvent être désactivées.

Pour placer plusieurs petits feux, ajouter plusieurs zones et choisir dans chacune l'animation déjà créée dans l'éditeur d'animations. Les coordonnées X/Y suivent le pont quand le bateau avance ou tourne. L'animation boucle pendant que la zone est active ; sa taille et ses couches proviennent de la définition d'animation. Une zone peut être uniquement visuelle en décochant son caractère réparable. Le seuil dépend de la santé globale de la coque, et non d'une réserve de points de vie indépendante par trou. Les effets sont dessinés sous les voiles et les cordages de premier plan.

Le mode de pont « Pêche » réserve les anciens emplacements de canon à la pêche et désactive leurs tirs. Choisir la zone dans le sélecteur alimenté par l'éditeur de pêche ; le bouton d'actualisation recharge cette liste. Le joueur doit s'approcher d'un poste libre et interagir avec E sur un bateau immobile. Le serveur conserve les règles de position de la zone et l'outil équipé, puis ouvre le mini-jeu existant avec ses prises, récompenses, progression et loot automatique. Le mouvement du bateau ou la perte des conditions de pêche interrompt la session. Le modèle dédié reste désarmé même si son ancien réglage de mode indique encore « Canons ».

Les offres de navire peuvent exiger le premium, une quête terminée et/ou un déblocage d'événement. Les conditions activées sont cumulatives et vérifiées côté serveur avant l'achat et le lancement. La commande « Offrir ou débloquer un navire » permet soit de débloquer le modèle pour la boutique, soit d'ajouter le navire à la flotte sans paiement ; l'attribution débloque aussi le modèle. Les conditions de navigation restent applicables. Les modèles débloqués sont enregistrés avec la carrière navale ; la flotte est restaurée si la sauvegarde échoue. Les répétitions d'une récompense ne créent pas de doublons.

## Rejouer le scénario de vérification

Le test serveur `SixPlayersWalkToFreeStationsOnATurningMovingShipAndKeepTheirOwnAim` fait réellement parcourir le masque aux six joueurs, par les paquets de déplacement, pendant que `ShipMovement.Step` avance et tourne le navire. Chaque joueur prend un poste différent et garde sa propre visée. Les snapshots sont sérialisés puis décodés par MessagePack, et le test écrit `deck-walk-replay.json` dans son dossier de sortie.

`Render-DeckReplay.ps1 -GameResources <resources-du-jeu> -OutputDirectory <sortie>` transforme ce replay en images avec des corps et équipements de démonstration issus du jeu. `pack-deck-apng.py <sortie/frames> <sortie/Deplacement_pont_75.png>` assemble l'aperçu animé sans modifier ses pixels. Le masque vert des huit vues montre les emplacements de pieds autorisés, y compris sous les voiles. Ce replay vérifie le résultat serveur hors ligne ; il ne constitue pas un essai de sept clients connectés ni une capture du client graphique final.

## Vérification

Les tests couvrent les postes du pilote et des canonniers, les passagers, les réservations expirées, la sérialisation réseau, les directions miroir, les coordonnées invalides, la restauration du personnage et le couple d’atlas fourni. Un aperçu hors ligne utilise les véritables feuilles de corps et d’équipement du jeu, avec et sans premier plan. Il ne remplace pas un essai multijoueur dans le client final.

Avant mise en production, vérifier les huit angles à l’arrêt et en mouvement, les changements de carte, l’entrée et la sortie d’équipage, le transfert de barre, les acquisitions/libérations de canons, les cheveux/capes/armes de différentes tailles, les effets de transformation et la visibilité furtive. Vérifier également le pilote occulté par la voile dans la vue de face et les toiles devant les mâts et échelles dans les deux diagonales de face.
