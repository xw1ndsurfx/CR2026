# Correctifs du playtest naval — 10 novembre 2026

- Les métadonnées des Ships of the Line dépassent 64 Kio. Le lecteur partagé client/éditeur utilise désormais la limite de 128 Kio déjà définie par ShipSpriteAtlas. Les 140 états des 35 modèles passent par le lecteur natif dans le test de régression.
- La boutique associe ses réponses navales au RequestId de sa propre demande. Une demande en attente ne peut plus intercepter l'ouverture du système naval. Le menu restaure également une fenêtre navale déjà ouverte mais minimisée.
- La perspective annule la pente diagonale des repères du pont par une transformation commune à la coque, aux couches supérieures et aux canons. Les mâts verticaux restent verticaux. Les huit orientations existantes restent utilisées ; les ressources sont toujours des sprites.
- La vue au canon recueille les commandes natives de brume, de surimpression, de lumière ambiante, de pluie et d'éclairs, puis les rejoue dans son rectangle. La vue compacte du capitaine est conservée. Les découpes de sources sont quantifiées avant redimensionnement pour éviter des raccords de brume de un pixel. L'option d'éclairage du joueur reste respectée.
- Les rencontres de PNJ relatives au joueur sont ignorées tant qu'il navigue. Les PNJ terrestres ne peuvent pas cibler un joueur embarqué. L'abordage conserve son combat dans une instance distincte, où les identifiants de navigation sont retirés. Le préambule d'ambiance des invasions ne déclenche pas de vagues dans le runtime ; ce correctif protège aussi contre les commandes de rencontre relatives des événements de contenu.
- La vitesse de marche sur le pont passe de 1,65 à 3 unités logiques par seconde. Les collisions de la barre et des canons sont réduites ; les mâts, tonneaux, limites de coque, autres joueurs, sous-pas et contrôles de temps serveur restent actifs.
- Le lancement recherche une place selon l'encombrement dessiné du pont et de la coque, avec contrôle des obstacles. Le rayon de recherche peut dépasser le réglage de base pour les grandes coques, avec une borne de 20 cases. Les distances d'embarquement et de débarquement suivent cet éloignement.
- Le serveur utilise les repères de canon embarqués depuis les métadonnées officielles pour placer les départs de projectiles et leurs effets. Les tirs individuels suivent également la visée de leur canon. Les anciens sprites conservent leur calcul de secours.

## Validation

Tests client/navals et composition d'ambiance ; tests serveur navals/invasions ; tests WinForms de l'éditeur naval, des boutiques, équipages et monstres ; régression GPU DesktopGL avec capture du framebuffer pour la brume à 0/128/255, la lumière ambiante et le cadrage de la vue compacte.

Le test GPU utilise Mesa depuis le seul répertoire des tests sur ce laptop. Cela vérifie le rendu et ne constitue pas une mesure de performances ni une session multijoueur sur le serveur de production. Aucun fichier du client installé, aucune base de contenu et aucun réglage de serveur en cours ne sont modifiés.

La branche part de main après la PR 262. Le commit privé du client Intel HD 3000 n'en fait pas partie.
