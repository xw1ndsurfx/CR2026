# Menu, boutiques et objectifs de donjon

- Le menu vertical en jeu contient **Lobby**, qui ouvre les lobbies d'aventure existants.
- La fiche personnage conserve les détails, succès, familiers et maîtrises. Les accès Jousting, Mounts et Archery en sont retirés.
- Les boutiques de sorts et de munitions proposent une recherche insensible à la casse et aux accents, les tris New/Old, niveau, prix croissant/décroissant et nom, ainsi que les filtres Tous, Disponibles, Possédés et Non possédés. New/Old utilise l'ordre des offres dans le catalogue : ces offres n'ont pas de date de création.
- Les sorts sont présentés sur deux colonnes de cartes. Les listes de munitions ont une taille de contenu explicite et gardent leurs commandes de défilement pendant les mises à jour.

## Écuries

Dans l'éditeur d'événements, ajouter **Open Stable** dans la catégorie des boutiques et banques, sur le PNJ ou l'événement de l'écurie. Cette commande ouvre la fenêtre de gestion des montures.

Le serveur autorise la liste, la sélection et le changement de favori uniquement après cet événement, sur la même carte et dans la même instance, à deux cases au plus du point d'interaction. Descendre d'une monture reste possible après avoir quitté l'écurie. Débloquer une monture via un objet ne donne pas d'accès distant à la fenêtre.

Les entrées de joute et les places de spectateurs existantes restent utilisables sur place. Le tir à l'arc s'ouvre via la commande de mini-jeu Archery existante, à placer sur un événement du lieu. Les vérifications de position au démarrage des activités restent appliquées par le serveur.

## Objectifs de donjon

Dans l'onglet Completion de l'éditeur de donjons :

1. Cocher **Terminer la quête sélectionnée**.
2. Choisir la **Quête à terminer**.
3. Conserver ou décocher les autres objectifs selon le scénario voulu.

Un participant doit terminer cette quête pendant la partie, à l'intérieur des cartes du donjon et dans la bonne instance. Une quête terminée avant l'entrée ne valide pas cet objectif. La progression est partagée par le groupe ; les objectifs cochés doivent tous être remplis. Les quêtes répétables et les complétions par événement sont prises en charge.

La nouvelle sélection est distincte de la quête associée et de la quête nécessaire pour entrer. Les donjons existants gardent leurs objectifs actuels. Le client, le serveur et l'éditeur doivent être reconstruits ensemble ; les événements d'écurie et les objectifs doivent ensuite être configurés dans les données du jeu.
