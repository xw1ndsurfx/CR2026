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

Une fois assis, le spectateur voit automatiquement un gros plan frontal à partir de la charge, puis la visée, l’impact et les résultats de la passe. La fenêtre reprend le composant graphique des participants, sans réticule ni commande de visée. Pendant la charge, le chevalier approche par agrandissement du sprite ; les images d’impact, de lance brisée et de désarçonnement viennent des résultats autoritaires du duel. Les images de prototype existantes sont réutilisées ; cet ajout ne remplace pas les retouches artistiques finales.

La position assise, la réservation et la visibilité sur la carte restent inchangées. Les caméras **chevalier doré / bleu** sélectionnent aussi le point de vue correspondant dans le gros plan, en PvE comme en PvP. La caméra **arène** reprend le point de vue doré dans le gros plan et le cadrage natif de la lice derrière la fenêtre. Les boutons de caméra et **Se lever** restent accessibles sous l’image. Fermer la fenêtre ne libère pas la place et elle reste fermée pour le reste de cette passe ; la suivante la rouvre. Se lever, changer de carte ou terminer le duel retire le gros plan.

Le serveur publie phase, passe, durée, temps restant, scores et frappes résolues. Il ne diffuse ni les coordonnées de visée courantes ni l’état de défense des participants. Les instantanés destinés aux joueurs assis sont actualisés au plus toutes les 100 ms pendant le spectacle ; le client fait progresser le compteur entre deux messages sans calculer de résultat. Un visiteur non assis ne reçoit pas de présentation 2,5D. Aucune scène ou caméra cinématique parallèle n’est créée.

Mettre à jour **le client et le serveur** pour activer cette présentation. Un client récent connecté à un ancien serveur conserve la fenêtre de places et les scores, mais ne reçoit pas le nouveau contenu de gros plan. La validation locale couvre les parcours serveur PvE/PvP, les deux perspectives, la sortie du siège, les contrôles en en/fr/es et le rendu GPU de William/Night avec les vrais PNG. La recette avec plusieurs clients connectés reste à effectuer.

## Posture et limites de validation

La posture assise est composée à l’affichage à partir du sprite réel et des couches d’équipement, avec un léger mouvement de repos. Elle ne repose pas sur une nouvelle feuille d’animation dessinée par l’artiste. Une retouche artistique et une recette graphique sur une vraie carte restent nécessaires. Les images du décor existant restent sur la grille 16 × 16 ; cette étape n’ajoute pas le tileset final complet.

Les tests natifs couvrent réservations concurrentes, identité des personnages, blocages de déplacement/combat/voyage, PNJ remplacés/restaurés, libération des réservations, scores publics, invalidité des sièges et géométrie commune du corps et des équipements. Le formulaire et la fenêtre sont exercés en français, anglais et espagnol. Les transports de test ne remplacent pas la recette manuelle avec deux clients connectés. Les cérémonies royales finales, le décor complet, les tournois et les intégrations quêtes/achievements/classements restent à terminer.
