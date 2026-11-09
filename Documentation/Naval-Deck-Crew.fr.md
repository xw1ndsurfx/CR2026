# Équipage visible et premier plan du bateau

Le rendu monde dessine la base du bateau, les personnages équipés, puis une texture de premier plan. Les voiles, mâts, haubans, canons et rambardes proches peuvent ainsi masquer les personnages. Les textures de premier plan utilisent des pixels du dessin original sélectionnés par des masques ; elles ne redessinent pas un second bateau décalé.

## Activer le trois-mâts

Dans la définition du navire, sélectionner `Ship_CorpsRoyaux_ThreeMast_Layered_v3_compat.png`, quatre images et 180 ms par image. Les deux PNG et le `.png.naval.json` correspondants sont fournis dans `resources/ocean`. Régler la capacité à huit si l’on veut accueillir un pilote, six canonniers et un passager. Les dimensions de collision restent un réglage distinct : elles doivent être adaptées et essayées au quai pour ce grand navire. Aucun ancien navire n’est remplacé automatiquement.

Le pack compact fait 3072 × 1024, avec `PixelsPerTile = 24`. Le pack maître fait 6144 × 2048, avec `PixelsPerTile = 48` : les deux occupent exactement la même taille dans le monde. Les quatre phases font légèrement flotter le bateau ; elles ne déforment pas la toile.

Ne pas sélectionner la texture `fringe` comme bateau autonome. Elle est chargée via `ForegroundGraphics` dans les métadonnées de la base. Ne pas réutiliser l’ancien petit idle ou un ancien sprite de dégâts pour ce nouveau modèle : les couches et ancrages doivent être préparés ensemble pour chaque état graphique.

## Métadonnées et synchronisation

`ForegroundGraphics` indique une texture Ocean de mêmes dimensions que la base. Chaque image dispose de `CrewAnchors`, dont les coordonnées sont locales au rectangle de l’image : position des pieds, direction du personnage, rôle et index du poste. Les rôles sont Helm, PortCannon, StarboardCannon et Passenger. Les ancrages changent avec l’angle et suivent la même phase de flottement que les deux textures. Un nom de ressource hors du dossier Ocean, des coordonnées invalides et des postes répétés sont refusés.

Le serveur ajoute les occupants des canons aux snapshots des bateaux. Il n’expose ni jeton de réservation ni données privées supplémentaires. Seuls les postes actifs, non expirés, appartenant encore à l’équipage sont envoyés. Le pilote réel reçoit le poste de barre, y compris après un transfert. Les membres sans canon sont placés sur les ancrages passagers disponibles.

Le client réutilise le rendu du personnage déjà reçu : corps, coiffure, apparence, équipement, transformation et règles de visibilité. Il ne peint pas d’avatars dans la texture du navire et n’invente pas d’apparence si une entité manque. La position et la direction de dessin sont temporaires ; les coordonnées réseau et les limites d’interaction sont restaurées même si le dessin échoue. Les montures et poses assises ne sont pas dessinées sur le pont.

Si la couche de premier plan ou les ancrages manquent, les anciens bateaux conservent leur rendu précédent. Les vues de canon en perspective gardent leur présentation existante ; ce raccordement concerne le rendu monde en 2D.

## Vérification

Les tests couvrent les postes du pilote et des canonniers, les passagers, les réservations expirées, la sérialisation réseau, les directions miroir, les coordonnées invalides, la restauration du personnage et le couple d’atlas fourni. Un aperçu hors ligne utilise les véritables feuilles de corps et d’équipement du jeu, avec et sans premier plan. Il ne remplace pas un essai multijoueur dans le client final.

Avant mise en production, vérifier les huit angles à l’arrêt et en mouvement, les changements de carte, l’entrée et la sortie d’équipage, le transfert de barre, les acquisitions/libérations de canons, les cheveux/capes/armes de différentes tailles, les effets de transformation et la visibilité furtive. Vérifier également le pilote occulté par la voile dans la vue de face et les toiles devant les mâts et échelles dans les deux diagonales de face.
