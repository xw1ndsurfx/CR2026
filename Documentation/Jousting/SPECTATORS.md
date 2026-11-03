# Joutes Royales — gradins et spectateurs

Cette étape ajoute les places réservées côté serveur, les joueurs réels assis avec leurs équipements, les PNJ de remplissage et le suivi de la joute. Elle complète les duels PvP et l’économie existants ; elle ne termine pas tout le cahier V2.

## Configurer les gradins

1. Ouvrir la carte de l’arène puis Mini-Games → Jousting Editor. Les montures et scènes se sélectionnent par leur nom dans les premiers onglets.
2. Dans **6. Places**, ajouter une place et lui donner un nom. Cliquer **Placer la place**, puis sur la case du banc dans l’aperçu. Cliquer **Placer l’accès**, puis sur une case libre directement adjacente. Le banc apparaît en vert, l’accès en blanc.
3. Choisir l’orientation du personnage. Le banc et l’accès doivent rester hors des deux couloirs. Deux places ne peuvent pas partager un banc ; un accès ne peut pas être un banc. Les accès peuvent être partagés si la géométrie le permet.
4. Facultativement, choisir un PNJ de foule par son nom et régler le zoom spectateur. Son sprite remplit les places libres ; ce sont des acteurs décoratifs, sans IA de combat, butin ni récompense. Chaque arène accepte 64 places, dans une limite de 512 places configurées au total.
5. Enregistrer l’arène côté serveur. Sur le banc, seuls l’absence d’attribut et l’attribut de blocage sont permis. L’accès doit être dépourvu d’attribut. Un banc bloqué convient à un décor solide ; le serveur ne permet le transfert d’une case que depuis son accès configuré.

## Assister à une joute

Marcher sur l’accès configuré d’un gradin : la fenêtre des places s’ouvre automatiquement. Le raccourci Lobby → Joutes Royales → Gradins de la joute reste également disponible. Choisir la place par son nom ; son accès est affiché. Se placer sur cet accès puis cliquer **S’asseoir à cette place**. La réservation et le déplacement sont contrôlés par le serveur. Le personnage réel, son apparence et ses équipements restent visibles pour les autres joueurs. Le PNJ qui occupait la place disparaît pendant la réservation.

Les boutons proposent la vue de l’arène ou le suivi du chevalier doré/bleu. Ils utilisent la caméra cinématique native. Les scores et les passes viennent de la simulation serveur PvE/PvP ; la visée et la défense des chevaliers ne sont pas diffusées aux spectateurs. Le dernier résultat reste visible vingt secondes. Fermer la fenêtre ne quitte pas le siège ; la rouvrir par Joutes Royales. Utiliser **Se lever** pour libérer la place et revenir à un accès libre. Si la sortie est bloquée, attendre ou dégager les cases alentour. Une déconnexion, un déplacement externe ou le retrait de la place libère la réservation. Une nouvelle cinématique native conserve la propriété de sa caméra.

Les émotes existantes restent celles du menu du chat et des common Events marqués comme émotes. Aucun système d’émotes parallèle n’est ajouté. Une émote qui lance une cinématique complète rend la place au système de scène.

## Vue spectateur 2,5D

Une fois assis, le spectateur voit automatiquement une présentation 2,5D **plein écran dans le client** à partir de la charge, puis la visée, l’impact et les résultats de la passe. Le même composant passe en plein écran pour les participants pendant la visée, l’impact et les résultats. Le cadrage conserve ses proportions lors des changements de résolution et le pointeur utilise exactement la même transformation que les cibles. Le spectateur n’a ni réticule ni commande de visée.

Lors d’un désarçonnement, le plan de résolution montre les deux chevaliers : celui qui tombe passe de l’image de chute à l’image au sol et y reste pendant les résultats. Les deux frappes serveur sont examinées, ce qui couvre le joueur au premier plan, l’adversaire joueur, l’IA et la double chute. Les noms des personnages au sol sont affichés. Ces poses réutilisent les images de prototype existantes ; cet ajout ne remplace pas les retouches artistiques finales ni n’ajoute une posture au sol au personnage sur la carte 2D.

La position assise, la réservation et la visibilité sur la carte restent inchangées. Les caméras **chevalier doré / bleu** sélectionnent aussi le point de vue correspondant dans le gros plan, en PvE comme en PvP. Les boutons de caméra, **Se lever** et **Commandes des gradins** restent accessibles en bas du plein écran. Ce dernier bouton revient à la gestion sans quitter la place ; la passe suivante rouvre le spectacle. La fin du duel ou la sortie du siège restaure la gestion ordinaire. La caméra native continue à cadrer la carte derrière la présentation.

## Sons configurables

Dans **7. Sons** du Jousting Editor, sélectionner un fichier par événement et son volume de 0 à 100 %. Les choix proviennent des ressources sonores locales du jeu ; un fichier absent reste nommé comme introuvable. Le choix **Aucun** laisse l’événement silencieux. Les anciennes arènes restent silencieuses tant qu’aucun son n’est affecté.

Les 19 événements sont : présentation, compte à rebours, début de charge, galop, entrée en visée, impact général, bouclier, plastron, coup manqué, bris de lance, désarçonnement, chute au sol, résultats de passe, victoire, défaite, égalité, interruption, installation dans les gradins et ambiance de foule. Le galop et la foule bouclent automatiquement. Les autres sons ne se répètent pas à chaque actualisation réseau ou changement de point de vue. La sortie arrête les boucles appartenant à cette joute, sans arrêter les autres sons du jeu. Les événements et sons du Cinematic Director restent utilisables pour les cérémonies plus détaillées.

Le serveur publie phase, passe, durée, temps restant, scores et frappes résolues. Il ne diffuse ni les coordonnées de visée courantes ni l’état de défense des participants. Les instantanés destinés aux joueurs assis sont actualisés au plus toutes les 100 ms pendant le spectacle ; le client fait progresser le compteur entre deux messages sans calculer de résultat. Un visiteur non assis ne reçoit pas de présentation 2,5D. Aucune scène ou caméra cinématique parallèle n’est créée.

Mettre à jour **le client et le serveur** pour activer cette présentation. Un client récent connecté à un ancien serveur conserve la fenêtre de places et les scores, mais ne reçoit pas le nouveau contenu de gros plan. La validation locale couvre les parcours serveur PvE/PvP, les deux perspectives, la sortie du siège, les contrôles en en/fr/es et le rendu GPU de William/Night avec les vrais PNG. La recette avec plusieurs clients connectés reste à effectuer.

## Posture et limites de validation

Le rendu 2,5D général reprend désormais aussi l’ancrage des entités `Resource` : arbres, rochers et autres ressources passent par leur méthode de dessin réelle avant le rendu en perspective. Un test GPU vérifie leur collecte et leur présence dans les pixels rendus. Les ressources masquées restent masquées. Cette correction concerne les ressources-entités de la carte, sans ajouter un décor à l’image frontale de la joute.

Les images fournies sous licence restent dans le paquet local. Les tests graphiques qui utilisent William/Night portent la catégorie `LicensedAssets` et sont exécutés localement avec ce paquet. La CI publique exécute les tests GPU de terrain, d’atmosphère et de ressources avec des textures de test générées ; elle ne publie pas les planches source de l’artiste.

La posture assise est composée à l’affichage à partir du sprite réel et des couches d’équipement, avec un léger mouvement de repos. Elle ne repose pas sur une nouvelle feuille d’animation dessinée par l’artiste. Une retouche artistique et une recette graphique sur une vraie carte restent nécessaires. Les images du décor existant restent sur la grille 16 × 16 ; cette étape n’ajoute pas le tileset final complet.

Les tests natifs couvrent réservations concurrentes, identité des personnages, blocages de déplacement/combat/voyage, PNJ remplacés/restaurés, libération des réservations, scores publics, invalidité des sièges et géométrie commune du corps et des équipements. Le formulaire et la fenêtre sont exercés en français, anglais et espagnol. Les transports de test ne remplacent pas la recette manuelle avec deux clients connectés. Les cérémonies royales finales, le décor complet, les tournois et les intégrations quêtes/achievements/classements restent à terminer.
