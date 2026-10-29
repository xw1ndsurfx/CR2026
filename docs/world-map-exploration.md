# Brouillard d’exploration

La carte du monde dévoile chaque carte du moteur à la première visite du personnage.
Une région découverte reste visible après un départ, une mort, un changement de grille
ou une reconnexion. Les découvertes sont propres au personnage, pas au compte ou au groupe.

Les cartes inconnues sont recouvertes d’un voile opaque avec le libellé « Région
inexplorée ». Leurs noms d’événements, animations, joueurs et navires ne sont pas
affichés. Le calcul des trajets de quête de l’atlas reste dans les cartes découvertes ;
aucun trajet n’est dessiné vers une destination encore inconnue. Zoom et déplacement
de la carte restent disponibles. La minimap conserve ses alentours immédiats.

## Acquisition et sauvegarde

- Une arrivée validée par le serveur découvre la carte : marche entre cartes,
  téléportation, navigation, entrée dans une instance ou connexion.
- Charger les cartes voisines, ouvrir l’atlas ou recevoir la position d’un ami ne
  découvre aucune région.
- Les GUID des cartes sont enregistrés dans une variable interne du personnage,
  `c2026029-0000-4000-8000-999999999999`, via la sauvegarde joueur existante. Aucun
  changement de schéma SQLite/MySQL. Comme les autres variables, les changements
  suivent les sauvegardes normales du serveur ; un arrêt brutal avant sauvegarde
  peut perdre les dernières découvertes.
- Les personnages existants commencent avec leur carte actuelle. Les anciennes
  visites ne peuvent pas être reconstituées à partir des seules données de position.
- Une carte renommée ou déplacée garde sa découverte grâce à son GUID. Une nouvelle
  carte avec un autre GUID nécessite une nouvelle visite.
- Le serveur transmet un état complet à la découverte, lors de l’envoi d’une grille
  et à l’ouverture de l’atlas. Il n’envoie les aperçus dédiés de l’atlas que pour les
  cartes découvertes. Le préchargement normal du jeu reste nécessaire aux déplacements :
  ce brouillard est une règle d’affichage, pas un mécanisme anti-extraction des données.

Les textes d’interface sont disponibles en anglais, français et espagnol dans le
catalogue client existant. Aucun service externe ni nouvel asset graphique.

## Mise à jour et vérification en jeu

Reconstruire et mettre à jour client et serveur ensemble : ajout du paquet
`WorldMapExplorationPacket`. L’éditeur conserve sa carte complète.

Sur une copie du serveur :

1. Connecter un nouveau personnage sur A, ouvrir l’atlas : A est visible, B voisine
   reste masquée malgré son préchargement. Zoomer et déplacer l’atlas.
2. Marcher de A à B, puis revenir : B se dévoile immédiatement et reste visible.
   Répéter avec téléportation, trajet naval et changement de grille.
3. Sauvegarder, reconnecter et redémarrer le serveur : A et B restent visibles.
4. Changer de personnage sur le même compte : aucune découverte n’est partagée.
5. Placer événements nommés, animations, amis et navires sur une carte inconnue :
   aucun marqueur de cette carte n’apparaît dans l’atlas. Vérifier aussi les grands
   marqueurs qui débordent d’une carte voisine et les trajets de quête.
6. Vérifier la minimap, l’éditeur et les deux nouveaux textes en EN/FR/ES.

Les tests automatisés couvrent la découverte côté serveur, la variable native sur
SQLite, le protocole et le filtrage du rendu. Ils ne remplacent pas le parcours
d’un client connecté avec les cartes et assets réels du jeu.
