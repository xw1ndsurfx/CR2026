# Joutes PvP — guide d’essai

Mettre à jour le serveur, le client et l’éditeur ensemble. Les ressources graphiques du paquet V3 restent utilisées ; cette version ne remplace pas les sprites par de nouveaux dessins.

## Configuration

Dans l’onglet **1. Arène**, cocher **Autoriser les duels entre joueurs**. L’entraînement contre l’IA se règle séparément. Une arène active doit autoriser au moins un de ces modes. Les anciennes configurations autorisent les deux par défaut ; aucun duel ne démarre sans invitation acceptée.

Conserver William ou une monture avec cavalier intégré, deux lignes distinctes et une distance horizontale paire. Les séquences d’introduction, de charge, d’impact et de victoire utilisent toujours les scènes et événements du Cinematic Director existant.

## Jouer avec deux comptes

1. Connecter deux personnages de **comptes différents** sur la même carte et la même instance. Aucun des deux ne doit être en combat, occupé par un autre mini-jeu ou retenu par un événement.
2. Le challenger se place au **départ doré** : `StartX, StartY`. Son adversaire se place au **départ bleu** : `EndX, OpponentY`. Les coordonnées sont affichées dans la fenêtre de duel.
3. Dans **Lobby → Joutes Royales**, ouvrir la fenêtre de joute puis **Défier un chevalier**.
4. Choisir le joueur par son nom puis **Défier ce joueur**. L’adversaire reçoit une invitation avec **Accepter** et **Refuser**. Elle expire après 30 secondes. Il est possible de fermer la fenêtre pour rejoindre la case bleue puis de la rouvrir depuis le même menu.
5. L’acceptation vérifie de nouveau les positions, la disponibilité, la monture, les deux comptes, les collisions et la réservation de la lice.
6. Les deux vrais personnages chargent sur la carte. Chaque joueur contrôle sa propre lance et sa défense pendant la visée. Le HUD indique son score et celui de l’adversaire nommé.

Le bouton **Quitter la joute** interrompt le duel. Le déplacement libre, les voyages du Lobby, le changement de monture et les attaques ordinaires sont bloqués pendant la session.

## Résultats et économie

Les deux lances sont résolues ensemble sur l’horloge serveur. Ni le côté du joueur, ni le résultat, ni l’heure d’impact ne sont fournis par le client. Les règles de points et le nombre de passes restent ceux de l’arène.

Une victoire PvP terminée rapporte 15 médailles si le joueur a marqué au moins un point. Une défaite terminée avec des points rapporte 1 médaille. Le plafond quotidien, le délai entre versements et l’interrupteur global d’économie de l’onglet **4. Gains** s’appliquent. Une interruption ou un abandon ne verse aucune médaille. Le résultat de chaque personnage utilise son propre côté et le même système de reçus persistants que le PvE.

Aucun droit d’entrée ni pari n’est prélevé. Les deux comptes distincts évitent les duels contre un second personnage du même compte ; ils ne constituent pas une détection des comptes alternatifs.

## Nettoyage

Déconnexion de l’un ou des deux joueurs, mort, changement de carte/instance, obstacle, suppression de la carte, abandon ou dépassement de l’échéance serveur interrompent la session. Les montures précédentes autorisées sont restaurées, les événements de la session sont arrêtés et la lice est libérée. Un contrôle périodique retire aussi les sessions dont les joueurs ne sont plus mis à jour.

Le serveur n’attend jamais le verrou d’un second personnage tout en bloquant le premier : il réessaie lors d’une mise à jour suivante. Les mouvements des deux participants et leur résolution partagent les mêmes verrous et la même simulation. Aucune copie PNJ du joueur adverse n’est créée ou détruite.

## Validation réalisée et limites

Les tests exécutent deux entités Player et leurs montures natives sur une MapInstance, trois passes complètes avec deux sources d’entrée, les scores relatifs, la victoire du deuxième côté, les interruptions, les invitations invalides et les acquisitions de verrous concurrentes. Les fenêtres sont exercées en anglais, français et espagnol, avec vérification du délai et du passage invitation → duel. Cela ne remplace pas un essai graphique manuel sur deux clients connectés : cet essai reste à effectuer.

Les sièges, les animations assises et la caméra spectateur, le décor complet, les cérémonies royales finalisées, les tournois et les intégrations dédiées de quêtes/achievements/classements restent à développer.
