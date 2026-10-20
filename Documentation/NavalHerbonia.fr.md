# Herbonia IA et retour au point d’embarquement

## Retour au quai
Le serveur mémorise la map et la case exacte occupée à la mise à l’eau. Après arrêt et débarquement près d’un quai Q valide, le joueur revient à cette case d’embarquement, même s’il accoste sur une autre map Ocean. La coque personnelle est rangée dans Ma flotte. Si la case est devenue bloquée/occupée, le serveur choisit une case libre adjacente au quai d’origine. Si le quai/la map ont disparu ou qu’aucune sortie n’est libre, le point de réapparition natif reste le dernier recours. Les anciennes flottes sans coordonnées précises gardent leur repli au quai. Le même retour s’applique après naufrage/reconnexion. Les navires publics mémorisent la case pendant la session uniquement.

## Configuration dans l’éditeur
1. Installer ensemble serveur, client, éditeur et leurs ressources; conserver les bases et configurations sauvegardées avant mise à jour. Aucun déploiement cloud n’est effectué par cette modification.
2. Ouvrir le System Editor naval. Dans Navigation et capitainerie, activer la navigation et les canons. Les dégâts entre joueurs peuvent rester désactivés : Herbonia est un ennemi PvE distinct.
3. Ouvrir Patrouilles IA Herbonia, puis Ajouter. Sélectionner une véritable map située dans Ocean (ou un sous-dossier). Choisir X/Y comme centre précis d’une zone d’eau libre, à plusieurs coques des quais et des bateaux existants. L’identifiant du bateau est créé par l’éditeur et ne doit pas être réutilisé.
4. Le modèle proposé utilise Ship_Herbonia_001.png et Ship_Herbonia_001_sink.png. Régler dimensions, vitesse et rotation comme pour les autres bateaux. Donner éventuellement un nom au navire. Aucun nom de personnage n’est créé pour l’IA.
5. Régler aussi le niveau (1 par défaut), les PV maximum de coque (100), les dégâts de canon (10) et l’EXP Sailing au naufrage (100). Le niveau est affiché au-dessus de l’ennemi; il ne multiplie pas automatiquement les statistiques, qui restent réglables indépendamment. Régler la portée de détection (12 cases par défaut), la distance maximale depuis le départ (24), le rayon de patrouille (5), la zone protégée autour des quais (6) et la réapparition après naufrage (60 secondes après la fin de l’animation). Mettre Activé à vrai et enregistrer. Arrêter les autres capitaines avant de modifier la configuration de navigation.
6. Commencer avec une seule patrouille. Aucun identifiant réel de map ni emplacement de production n’est inventé ici.

## Comportement
Une boucle serveur commune, limitée à 20 mises à jour par seconde, fait patrouiller les ennemis indépendamment des mises à jour des capitaines. Ils recherchent les bateaux pilotés à portée, poursuivent dans les limites de leur zone et orientent la coque pour tirer bâbord ou tribord. La trajectoire vers la cible doit être libre. Les canons, leurs rechargements, les sons et les effets utilisent le système existant. Les collisions balayées et les liens réciproques Ocean sont respectés. Les navires ennemis ne peuvent pas être pris à la barre.

Une zone proche des Q protège les capitaines des tirs ennemis, y compris lorsqu’ils y entrent après le départ du boulet. Les ennemis ne se blessent pas entre eux; leurs coques interceptent néanmoins les tirs. Les dégâts subis par les bateaux personnels suivent la sauvegarde serveur existante. Après naufrage, l’ennemi revient avec toute sa coque à son point initial, après le délai configuré et seulement si son emplacement est libre. Un ennemi vivant n’est jamais téléporté chez lui. Il revient en naviguant lorsque sa cible disparaît ou sort de sa zone.

Les cartes sans instance Overworld chargée ne sont pas créées par l’IA. Une patrouille apparaît/reprend lorsque le jeu charge sa zone; ses mouvements s’arrêtent si cette instance est déchargée. Une topologie modifiée qui ne permet plus de rejoindre le départ arrête le bateau. Redémarrer ou reconfigurer recrée les patrouilles à leurs positions initiales; la position et les PV ennemis ne sont pas persistants.

## Vérifications en jeu
- Embarquer depuis une case facile à repérer, naviguer, s’arrêter près d’un autre quai Q et utiliser 0/débarquer : retour à la case initiale. Refaire avec la case occupée : repli sûr au quai initial.
- Observer la patrouille, puis approcher à bord. Vérifier poursuite, orientation, bordées, sons/effets et baisse des PV sans activer le PvP.
- Revenir dans la zone de protection du quai. Vérifier l’arrêt des dégâts ennemis. Débarquer : aucun ciblage du joueur à pied.
- Tirer sur Herbonia jusqu’au naufrage, constater l’animation puis attendre la réapparition. Bloquer son emplacement initial pour vérifier qu’il attend sans apparaître dans un obstacle.
- Tester avec deux capitaines, des obstacles et une frontière Ocean réciproque. Vérifier que le temps de rechargement et la vitesse restent identiques avec plusieurs joueurs.

## Limites V1
L’évitement local tourne autour d’un obstacle; ce n’est pas une recherche de chemin complète dans des chenaux complexes. La recherche géographique est bornée aux maps voisines (deux frontières par axe). Limite de 32 patrouilles configurées. Le butin est configurable par patrouille (voir NavalCargo.fr.md). Pas encore de partage de récompense à l’équipage ou de factions configurables. Le niveau affiché et les statistiques se règlent indépendamment, sans formule automatique de difficulté.

Le PNG Herbonia 001 d’origine est copié sans retouche et accompagné de rectangles/ancrages explicites à quatre poses par direction logique. NE et SE utilisent respectivement NW et SW en miroir. L’inspection montre que les vues N/S de la source restent très semblables, et que certains sillages touchent la cellule voisine : une vraie vue arrière distincte et des poses mieux séparées sont nécessaires pour un rendu définitif. Le découpage n’est pas présenté comme une planche parfaitement alignée. La variante 002 n’est pas distribuée par cette modification. Le naufrage utilise la planche déjà vérifiée de la phase précédente.


## Progression et HUD de combat
L’EXP Sailing vient des ennemis coulés, jamais de la distance. La récompense est attribuée une fois au capitaine du tir fatal encore à sa barre; elle ne se partage pas entre participants. Les bateaux publics de test et les bateaux de joueurs ne donnent pas d’EXP. Une nouvelle incarnation d’ennemi après réapparition peut donner sa propre récompense. Les niveaux et l’EXP des personnages existants restent conservés.

La récompense est transmise à la mise à jour du personnage, puis sauvegardée avec les données navales existantes. Si la sauvegarde échoue, l’EXP est restaurée et la récompense reste en attente pour un nouvel essai, limité à une fois par seconde. Aucun verrou d’un autre joueur n’est pris pendant le combat. L’attente est en mémoire : un arrêt brutal avant la sauvegarde peut perdre une récompense non encore enregistrée.

Le HUD centré en haut affiche le nom, le niveau et les PV actuels/maximum de la coque ennemie. Une touche réussie choisit le dernier ennemi attaqué. Un tir ennemi reçu ouvre son HUD si aucune autre cible sortante n’est encore engagée. Le HUD disparaît au naufrage, en quittant la barre, au retour dans la zone protégée du quai, lorsque la cible n’est plus visible ou après le délai sans impact. Le délai est de 12 secondes par défaut, réglable dans Navigation et capitainerie (3 à 120 secondes). Une réapparition ne réactive pas une ancienne cible.

Pour tester : configurer un ennemi de niveau 8, 500 PV, 17 dégâts et 275 EXP; approcher et le toucher. Vérifier le niveau au-dessus du bateau et le HUD 500 PV maximum, puis la baisse réelle des PV. Naviguer/toucher sans couler ne doit pas modifier l’EXP. Couler cet ennemi doit donner 275 EXP une fois et masquer le HUD. Refaire avec deux capitaines : seul le coup fatal récompense. Quitter le combat et attendre le délai doit masquer le HUD; couler puis attendre la réapparition ne doit pas le rouvrir spontanément. Vérifier le gain et le niveau après reconnexion.

La récompense de base du naufrage alimente également l’EXP du personnage via le système natif (bonus et plafond de niveau habituels). La cargaison et le déchargement au quai sont décrits dans [Butin naval](NavalCargo.fr.md).
