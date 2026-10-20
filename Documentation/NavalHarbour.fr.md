# Capitainerie, boutique PNJ et System Editor

## Installation et compatibilité

Mettre à jour ensemble le serveur, le client, le Game Editor et les ressources Ocean issus de la même compilation. Les échanges de configuration navale et la commande d’événement `OpenNavalShop` sont nouveaux ; les fenêtres de flotte et de boutique échangent désormais un mode distinct. Aucun déploiement cloud n’est effectué par cette modification.

Les fichiers actifs du serveur restent `resources/naval.json` (navigation et navires de test) et `resources/naval-shop.json` (modèles et boutique). L’éditeur les lit et les enregistre à distance ; il n’est plus nécessaire de modifier leurs prix ou coordonnées à la main. Les exemples distribués n’écrasent pas les fichiers actifs. Une configuration illisible bloque son remplacement au lieu de produire un fichier vide.

## Configuration dans l’éditeur

Ouvrir **System Editor → Éditeur du système naval**. Les légendes sont disponibles en français, anglais et espagnol.

- **Navigation et capitainerie** : activation navale, portée d’interaction au quai, rayon de recherche pour la mise à l’eau, portée du PNJ de boutique et durée de validité de sa session.
- **Navires de test** : navires publics, map, centre précis X/Y en cases, cap, quai X/Y entier, dimensions de coque, sprites et paramètres de mouvement/animation. Les identifiants sont conservés. Choisir une map enregistrée sous le dossier Ocean ou ses sous-dossiers et une position libre pour la coque entière. Un quai ne doit pas être à l’intérieur d’une coque. La préparation automatique du menu contextuel des maps reste disponible.
- **Boutique navale et réparations** : nom et identifiant stable de chaque modèle, mise en vente, objet monnaie, Aureons/Auragems, prix d’achat, prix de réparation complète, Premium, sprites et caractéristiques du modèle. Les nouvelles offres sont initialement hors vente. Les positions de modèle ne s’éditent pas ici : elles sont calculées au lancement au quai.

**Enregistrer cette section** sauve la navigation et les navires de test depuis l’un des deux premiers onglets ; depuis le troisième onglet, il sauve le catalogue. Activer la navigation et la boutique séparément. La sauvegarde est atomique par fichier, confirmée par le serveur et protégée par une version indépendante pour chaque section. Deux éditeurs ne peuvent pas remplacer silencieusement les modifications de l’autre. Une réponse tardive n’efface pas l’état de modifications plus récentes. Fermer avec des changements non enregistrés demande de les abandonner.

Quitter toutes les barres avant de modifier les réglages de navigation : le serveur refuse de déplacer un capitaine en cours d’édition. Les navires de test inoccupés sont actualisés après une sauvegarde ; les navires personnels ne sont pas réinitialisés. Enregistrer les modifications des maps avant de régler les placements : la validation utilise les collisions enregistrées du serveur.

Pour retirer un modèle déjà publié, désactiver **En vente**. Le serveur conserve les identifiants du catalogue existant afin que les propriétaires puissent encore utiliser et réparer leurs bateaux. Le nombre de modèles reste limité à 32. Les fichiers de sprites doivent exister dans `resources/ocean`, avec leurs métadonnées de découpage vérifiées ; choisir un nom ne crée pas un asset. Le menu propose les sprites disposant d’un fichier `.naval.json` local.

## Fenêtre de flotte au quai

Dans **Map Attributes**, peindre **Q - Quai** sur les cases solides du quai, à côté des cases de rive accessibles au personnage, puis enregistrer la map. Le marqueur Q est destiné à l’éditeur : dessiner également le décor du quai avec les tilesets. Le Q bloque les personnages et les coques. Il remplace l’attribut précédent de la case.

La map doit être sous Ocean, sous-dossiers compris. À pied près du Q, utiliser l’interaction habituelle ou `/naval` pour ouvrir **Ma flotte**. Cette fenêtre contient uniquement les modèles possédés et l’action **Prendre la mer**. Elle ne propose aucun achat ni réparation. Le serveur choisit une zone libre près du quai, respecte les dimensions du navire et refuse les coques coulées et les restrictions Premium non satisfaites.

En navigation, arrêter avec Espace puis utiliser `/naval` près d’un Q libre : l’avatar débarque sur une case libre voisine d’un Q, hors des coques et obstacles. Le bateau personnel retourne dans sa flotte. Les bateaux de test publics ne deviennent pas la propriété du joueur ; leur embarquement historique reste disponible près de leur coque, hors de la portée d’un Q.

## Boutique et réparations ouvertes par un PNJ

Dans l’événement de map du PNJ, ouvrir la liste des commandes **Boutique et banque**, puis ajouter **Ouvrir la boutique navale et les réparations**. Cette commande n’a pas de paramètre : le catalogue est celui du System Editor. Faire terminer la page après l’ouverture, par exemple après le dialogue d’accueil. Un événement commun peut également l’ouvrir depuis la position courante du personnage.

La fenêtre **Boutique navale et réparations** propose uniquement **Acheter** et **Réparer entièrement**. Elle n’affiche pas l’action de mise à l’eau. Elle peut être ouverte par un PNJ de port hors des maps Ocean, en monde normal. Le serveur accorde une session liée à la map, à la position du PNJ et à une durée limitée. Une requête client seule, une session expirée, un changement de map ou un éloignement du PNJ ne permettent pas d’acheter ni de réparer. Une requête d’achat envoyée comme action de flotte, ou une mise à l’eau envoyée comme action de boutique, est rejetée.

Un objet monnaie explicite est prioritaire. Sans identifiant, le nom Aureons/Auragems doit correspondre à un seul objet Currency. Un nom absent ou ambigu ne débite rien. Le statut Premium utilise le système existant et reste vérifié sur le serveur.

Les propriétés de flotte restent enregistrées par personnage, avec l’inventaire dans la transaction native du compte. Un doublon de modèle, un manque de fonds ou une sauvegarde refusée ne doit pas débiter définitivement le joueur. Une réparation remet la coque à 100, même à partir de 0 ; elle ne lance pas le bateau.

## Correction du sud-est

Les anciens fichiers de découpage livrés pour les sprites connus utilisaient une pose arrière au sud-est. Les métadonnées livrées sont maintenant en version 2. Lors du chargement d’une ancienne version 1 reconnue, le client reconstruit la direction sud-est à partir de la pose sud-ouest retournée horizontalement, en conservant les ancrages et les autres directions. Il ne réécrit pas les ressources installées. Les atlas personnalisés et ceux déjà en version 2 ne sont pas migrés. Le correctif du rendu horizontal fait également partie du client correspondant.

## Test sur copie

1. Sauvegarder les bases et les deux fichiers navals du serveur de test. Installer les trois exécutables et les ressources correspondantes.
2. Ouvrir le System Editor naval. Vérifier les réglages existants, activer la navigation, configurer une offre avec une vraie monnaie et ses prix, cocher En vente et activer la boutique. Enregistrer chaque section.
3. Peindre et enregistrer un Q avec une rive accessible et une zone d’eau libre autour. Pour tester une transition, utiliser deux maps Ocean réellement reliées.
4. Configurer un événement PNJ avec la nouvelle commande. Lui parler : vérifier achat, absence d’action Prendre la mer, refus sans fonds ou sans Premium. Fermer et rouvrir pour vérifier la propriété.
5. Au Q, ouvrir Ma flotte : seul le bateau possédé doit être proposé, sans achat ni réparation. Prendre la mer et vérifier les huit directions, en particulier le sud-est, puis les collisions et transitions.
6. Revenir au quai, arrêter et débarquer. Vérifier la conservation de la propriété après déconnexion/reconnexion.
7. Depuis deux éditeurs, ouvrir la même section ; enregistrer une modification dans le premier, puis tenter de sauvegarder l’ancienne version du second : elle doit être refusée.

Les tests automatisés vérifient la sérialisation, les droits par fenêtre, l’expiration et la distance de session PNJ, les sauvegardes obsolètes, la conservation des données, les traductions, l’ancienne découpe sud-est et l’ouverture du formulaire natif. Le test connecté complet sur Ocean reste à exécuter sur la copie du jeu.

## Limites de la phase 1

Les canons V1 décrits plus bas déclenchent dégâts et naufrage avec `NavalSystem.DamageOwnedShip` pour les bateaux personnels. La navigation seule ne cause pas de dégâts. Les navires IA et dégâts de collision restent à développer. Un observateur arrivant en cours de naufrage peut encore voir son animation depuis le début. Aucun asset absent n’est présenté comme intégré.

## Quais Q bloquants et barre navale

Q se comporte désormais comme Block pour les personnages, les PNJ, les apparitions et les projectiles ordinaires. Les coques le respectaient déjà. On interagit avec le quai depuis une case voisine, sans marcher sur Q. Prévoir au moins une case **cardinale** libre à côté de Q, hors de la coque, pour débarquer. Le débarquement et le retour après naufrage choisissent une de ces cases voisines, jamais Q lui-même. Une zone Q complètement entourée d’obstacles ne permet pas de débarquer. Le recours au point de réapparition configuré reste utilisé si le quai de retour a été supprimé ou n’a plus de place.

Pendant le pilotage, les dix emplacements de raccourcis sont recouverts par une barre navale temporaire :

| Touche par défaut | Action |
| --- | --- |
| 1 | Bordée bâbord, à gauche du navire |
| 2 | Bordée tribord, à droite du navire |
| 3 à 8 | Réservées aux prochaines capacités navales |
| 9 | Activer/désactiver l’ancre ; W/S permet aussi de repartir |
| 0 | Quitter la barre, à l’arrêt près d’un quai accessible |

Les touches déjà personnalisées dans les réglages sont respectées et affichées. Les boutons sont également cliquables. La coque, la vitesse et le rechargement de chaque bordée sont affichés. Aucun objet/sort du personnage n’est déplacé, supprimé ou remplacé dans ses raccourcis enregistrés. La barre habituelle revient après débarquement ou naufrage. La saisie dans le chat, les dialogues et le glisser-déposer ne déclenchent pas les attaques.

## Canons V1

Dans **System Editor → Éditeur du système naval → Navigation et capitainerie**, activer **Activer les canons**, puis sauvegarder la section. Les configurations existantes conservent les canons désactivés tant qu’un éditeur n’a pas activé ce réglage. Arrêter tous les capitaines avant de changer cette section, comme pour les autres réglages de navigation.

Paramètres modifiables : dégâts par boulet (1 à 100), rechargement indépendant de chaque bordée (1 000 à 60 000 ms), portée (1 à 40 cases depuis la bouche du canon), vitesse (1 à 30 cases/s). Valeurs initiales : 10 dégâts, 4 000 ms, 12 cases, 12 cases/s. Chaque bordée tire trois boulets parallèles. Les canons utilisent le cap réel continu du navire ; le choix du sprite n’influence pas la trajectoire.

**Dégâts entre bateaux de joueurs** est désactivé par défaut, séparément des canons. Les bateaux personnels et les navires publics pilotés sont alors protégés, mais interceptent les boulets. Les navires publics inoccupés servent de cibles de test. Activer cette option permet les dégâts navals entre capitaines sur Ocean ; elle n’ajoute pas de zones ou de règles de duel. Aucun dégât n’est appliqué directement aux personnages à pied.

Les positions, trajectoires, collisions, rechargements et dégâts sont calculés par le serveur. Un client demande une action et un identifiant de bateau ; il ne fournit pas de position de tir, de cible, de prix ou de dégâts. Les boulets traversent les frontières de maps Ocean réellement reliées, s’arrêtent sur les obstacles/limites et effectuent des pas de collision courts. La collision de coque est conservatrice, comme pour la navigation. Les dégâts d’un bateau personnel passent par la sauvegarde de flotte existante sur son propriétaire, avant son prochain débarquement ; les impacts ne prennent pas le verrou d’un autre capitaine depuis le tireur. Un échec de sauvegarde entraîne une nouvelle tentative espacée.

À zéro coque, le navire joue son naufrage. Un bateau personnel reste possédé, avec zéro coque, et doit être réparé au PNJ. Un navire public de test coulé disparaît après son animation ; recharger la configuration navale ou redémarrer le serveur le recrée. Le sprite de naufrage des navires de test est configurable ; celui des bateaux personnels reste défini dans le catalogue.

Les effets utilisent maintenant les deux planches fournies : `Ship_Canon_Fire_Animation.png` et `Ship_Canon_Fire_impact.png`, avec leurs fichiers `.effects.json`. Les boulets gardent un rendu simple si leur ressource est absente. Il n’y a pas encore de munitions consommables, d’IA ennemie, de visée immersive, d’équipage ou de capacités sur les emplacements 3 à 8.

### Vérification connectée sur une copie

1. Mettre à jour ensemble le client, le serveur et l’éditeur de cette branche, les paquets réseau ayant changé. Ne pas modifier le serveur de production pour ce test.
2. Peindre Q sur une map Ocean, enregistrer la map, puis vérifier qu’un personnage ne peut pas entrer dans Q. L’interaction depuis une case voisine doit toujours ouvrir Ma flotte.
3. Enregistrer au moins une case voisine libre du Q. Mettre son bateau à l’eau : la barre navale apparaît, la coque et la vitesse sont visibles. Vérifier que 3 à 8 n’utilisent aucun objet ou sort habituel.
4. Vérifier 9 pour l’ancre, puis W/S pour repartir. Revenir au Q et utiliser 0 à l’arrêt : débarquement sur une case libre voisine et retour des raccourcis habituels. Bloquer toutes les sorties et vérifier le refus de débarquer.
5. Dans le System Editor, tous les capitaines à terre, activer les canons et garder les dégâts entre joueurs désactivés. Placer un deuxième navire public de test inoccupé, avec une coque libre d’obstacles, à portée latérale du bateau du joueur.
6. Piloter puis tirer avec 1 et 2 : trois boulets par bordée, trajectoires latérales et rechargements indépendants. Les tirs répétés pendant le rechargement ne créent pas de boulets supplémentaires. Tester un obstacle et une liaison Ocean à Ocean.
7. Toucher la cible publique jusqu’au naufrage. Pour vérifier les bateaux personnels et leur réparation, activer explicitement les dégâts entre joueurs sur la copie, puis tester avec deux capitaines : coque diminuée, naufrage, retour au quai, réparation au PNJ et reprise de la mer avec 100 de coque.
8. Vérifier aussi l’ouverture du chat, les touches personnalisées et le retour de la barre normale après reconnexion. Le parcours connecté et le rendu réel du HUD restent à valider dans le jeu installé.

## Sons, icônes, noms et progression Sailing

Le System Editor naval comporte cinq onglets. Les nouveaux onglets **Sons, icônes et effets** et **Progression Sailing** se sauvegardent avec la configuration de navigation, indépendamment de la boutique. Comme pour cette configuration, tous les capitaines doivent quitter la barre avant l’enregistrement.

### Ressources sélectionnables

- Les quatre icônes d’action (bâbord, tribord, ancre, débarquement) se choisissent dans **resources/spells**. Un aperçu de l’image sélectionnée apparaît à droite du choix. Un choix vide conserve l’icône native dessinée par le client.
- Les sons se choisissent dans **resources/sounds** au format **WAV**, celui que charge le moteur pour ses effets sonores. Le bouton **Écouter** permet une écoute dans l’éditeur. Les fichiers OGG de musique ne sont pas proposés comme effets sonores.
- Chaque événement a son choix : tir, impact sur coque, impact dans l’eau, impact sur obstacle, naufrage, ancre, mise à l’eau, débarquement et niveau Sailing. Un champ vide désactive ce son. Aucun fichier sonore absent n’a été inventé ou ajouté.
- Une bordée joue son son de tir une fois, avec trois effets visuels. Les impacts ont leurs propres événements. Les sons utilisent la distance et les réglages audio natifs du client ; les événements répétés dans les mises à jour réseau ne rejouent pas leur son.
- Les animations se choisissent dans **resources/ocean** parmi les fichiers dotés d’un découpage `.effects.json` compatible avec le rôle choisi. La durée d’une image et l’échelle (pixels par case) sont modifiables dans l’éditeur. Une valeur élevée de pixels par case réduit la taille affichée.

Les PNG fournis sont copiés sans modification. La première planche utilise ses **trois premiers éclats distincts** pour le tir, orientés autour de leur point d’ancrage. Les autres rangées de fumée de cette planche ne sont pas utilisées. La seconde fournit **huit directions de boulet, quatre images par direction**, puis **quatre images d’impact sur l’eau** et **quatre sur coque**. Les découpages sont explicites et compensent les ancrages différents ; aucune grille régulière n’est supposée. Les `.effects.json` font partie des ressources à livrer avec les PNG. Pour une nouvelle planche personnalisée, un découpage compatible doit également être créé ; le choix d’un fichier dans l’éditeur ne découpe pas automatiquement une image.

### Nom du bateau

Dans **Ma flotte**, le bouton **Nommer le bateau** permet de choisir un nom avant la mise à l’eau, près de Q. Le serveur vérifie le propriétaire et le quai, puis sauvegarde le nom. Un échec de sauvegarde conserve l’ancien nom. Le nom contient 1 à 32 lettres/chiffres, espaces, apostrophes, points ou tirets. Le client affiche ce nom au-dessus de la coque pour tous les observateurs. Il ne traduit pas les noms choisis par les joueurs. Les bateaux non nommés conservent le nom du modèle dans Ma flotte ; aucun nom personnel n’est inventé. Les navires publics de test ont aussi un champ Nom dans l’éditeur.

### Sailing

La progression est **par personnage**, commune à ses bateaux. Une barre bleu/or avec le niveau et l’EXP se place juste au-dessus des actions navales. La coque et la vitesse restent au-dessus de cette barre.

L’EXP vient uniquement de la distance réellement acceptée par le serveur pendant la navigation sur Ocean. Une rotation sur place, l’ancre, un obstacle qui empêche d’avancer, un changement de map par téléportation ou une requête client ne donnent pas d’EXP. Les fractions de distance sont cumulées pendant la sortie en mer. Il n’y a pas encore de bonus d’EXP de combat.

Valeurs initiales : **1 EXP par case parcourue**, **100 EXP pour passer du niveau 1 au 2**, multiplication par **1,12** pour chaque niveau suivant, maximum **100 niveaux**. Tous ces paramètres se modifient dans Progression Sailing. Une valeur d’EXP par case à zéro suspend les gains. La progression se conserve lors d’un changement de bateau, d’une réparation ou d’un naufrage. Réduire temporairement le plafond ne détruit pas l’EXP déjà gagnée.

Le stockage naval existant du personnage contient désormais une enveloppe version 2 avec les navires, leurs noms et l’EXP Sailing. Les anciennes listes de flotte sont lues sans perdre les identifiants, coques ou points de retour. Aucun changement de schéma SQL n’est nécessaire. Les sauvegardes natives incluent cette donnée ; une sauvegarde est également demandée à la sortie de la barre et lors du renommage. **Sauvegarder les bases avant la mise à jour et garder cette copie pour un éventuel retour à un ancien serveur**, qui ne connaît pas cette nouvelle enveloppe.

### Vérification de cette tranche

1. Sur le serveur de test, sauvegarder les bases, puis installer ensemble serveur, client, éditeur, les deux PNG de canon et leurs `.effects.json`. Les ressources d’icônes/sons sélectionnées doivent exister sur le client et dans le dossier de ressources de l’éditeur.
2. Dans Sons, icônes et effets, choisir une icône de resources/spells : vérifier son aperçu. Choisir un WAV, l’écouter, sélectionner les autres sons désirés, enregistrer, fermer et rouvrir pour vérifier les choix.
3. À Q, nommer un bateau, fermer/rouvrir Ma flotte puis se reconnecter : le nom doit être conservé. Prendre la mer et vérifier son nom dans les huit directions, depuis un second client si disponible.
4. Tirer des deux côtés : les icônes choisies sont utilisées, les éclats se produisent aux canons, les boulets suivent leur direction. Tester une cible, un obstacle et un tir sans cible jusqu’à sa chute dans l’eau. Chaque type d’impact doit utiliser son son ; une bordée ne doit pas rejouer son son à chaque mise à jour.
5. Observer Sailing : progresser en naviguant, s’arrêter contre un obstacle et à l’ancre pour vérifier l’absence de gains immobiles. Passer un niveau, débarquer, changer de bateau et se reconnecter : le niveau et l’EXP doivent rester présents. Vérifier aussi une traversée Ocean à Ocean.
6. Les tests automatiques couvrent migration des données, noms et retour arrière de sauvegarde, seuils/cap d’EXP, distance fractionnaire, paquets, choix et aperçu d’icône, découpages et ancrages tournés, réception répétée d’un effet. Le rendu connecté et l’écoute des vrais sons sélectionnés restent à vérifier dans le jeu installé.


Suite : [Patrouilles IA Herbonia et retour à l’embarquement](NavalHerbonia.fr.md).
