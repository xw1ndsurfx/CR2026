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

Dans **Map Attributes**, peindre **Q - Quai** sur les cases de rive où le personnage peut se tenir et débarquer, puis enregistrer la map. Le marqueur Q est destiné à l’éditeur : dessiner également le décor du quai avec les tilesets. Le Q est praticable à pied mais bloque la coque. Il remplace l’attribut précédent de la case.

La map doit être sous Ocean, sous-dossiers compris. À pied près du Q, utiliser l’interaction habituelle ou `/naval` pour ouvrir **Ma flotte**. Cette fenêtre contient uniquement les modèles possédés et l’action **Prendre la mer**. Elle ne propose aucun achat ni réparation. Le serveur choisit une zone libre près du quai, respecte les dimensions du navire et refuse les coques coulées et les restrictions Premium non satisfaites.

En navigation, arrêter avec Espace puis utiliser `/naval` près d’un Q libre : l’avatar débarque sur une case Q hors des coques et obstacles. Le bateau personnel retourne dans sa flotte. Les bateaux de test publics ne deviennent pas la propriété du joueur ; leur embarquement historique reste disponible près de leur coque, hors de la portée d’un Q.

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

Les canons, navires IA, dégâts de collision et combats ne sont pas raccordés. Le point d’entrée serveur `NavalSystem.DamageOwnedShip` peut déclencher dégâts et naufrage, mais la navigation seule ne cause pas de dégâts. La réparation d’un bateau coulé est couverte automatiquement ; un essai de naufrage en jeu attend les futurs combats. Un observateur arrivant en cours de naufrage peut encore voir son animation depuis le début. Aucun asset absent n’est présenté comme intégré.
