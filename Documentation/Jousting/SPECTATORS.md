# Joutes Royales — gradins et spectateurs

Cette étape ajoute les places réservées côté serveur, les joueurs réels assis avec leurs équipements, les PNJ de remplissage et le suivi de la joute. Elle complète les duels PvP et l’économie existants ; elle ne termine pas tout le cahier V2.

## Configurer les gradins

1. Ouvrir la carte de l’arène puis Mini-Games → Jousting Editor. Les montures et scènes se sélectionnent par leur nom dans les premiers onglets.
2. Dans **6. Places**, ajouter une place et lui donner un nom. Cliquer **Placer la place**, puis sur la case du banc dans l’aperçu. Cliquer **Placer l’accès**, puis sur une case libre directement adjacente. Le banc apparaît en vert, l’accès en blanc.
3. Choisir l’orientation du personnage. Le banc et l’accès doivent rester hors des deux couloirs. Deux places ne peuvent pas partager un banc ; un accès ne peut pas être un banc. Les accès peuvent être partagés si la géométrie le permet.
4. Facultativement, choisir un PNJ de foule par son nom et régler le zoom spectateur. Son sprite remplit les places libres ; ce sont des acteurs décoratifs, sans IA de combat, butin ni récompense. Chaque arène accepte 64 places, dans une limite de 512 places configurées au total.
5. Enregistrer l’arène côté serveur. Sur le banc, seuls l’absence d’attribut et l’attribut de blocage sont permis. L’accès doit être dépourvu d’attribut. Un banc bloqué convient à un décor solide ; le serveur ne permet le transfert d’une case que depuis son accès configuré.

## Assister à une joute

Dans le Lobby, ouvrir Joutes Royales puis **Gradins de la joute**. Choisir la place par son nom ; son accès est affiché. Se placer sur cet accès puis cliquer **S’asseoir à cette place**. La réservation et le déplacement sont contrôlés par le serveur. Le personnage réel, son apparence et ses équipements restent visibles pour les autres joueurs. Le PNJ qui occupait la place disparaît pendant la réservation.

Les boutons proposent la vue de l’arène ou le suivi du chevalier doré/bleu. Ils utilisent la caméra cinématique native. Les scores et les passes viennent de la simulation serveur PvE/PvP ; la visée et la défense des chevaliers ne sont pas diffusées aux spectateurs. Le dernier résultat reste visible vingt secondes. Fermer la fenêtre ne quitte pas le siège ; la rouvrir par Joutes Royales. Utiliser **Se lever** pour libérer la place et revenir à un accès libre. Si la sortie est bloquée, attendre ou dégager les cases alentour. Une déconnexion, un déplacement externe ou le retrait de la place libère la réservation. Une nouvelle cinématique native conserve la propriété de sa caméra.

Les émotes existantes restent celles du menu du chat et des common Events marqués comme émotes. Aucun système d’émotes parallèle n’est ajouté. Une émote qui lance une cinématique complète rend la place au système de scène.

## Posture et limites de validation

La posture assise est composée à l’affichage à partir du sprite réel et des couches d’équipement, avec un léger mouvement de repos. Elle ne repose pas sur une nouvelle feuille d’animation dessinée par l’artiste. Une retouche artistique et une recette graphique sur une vraie carte restent nécessaires. Les images du décor existant restent sur la grille 16 × 16 ; cette étape n’ajoute pas le tileset final complet.

Les tests natifs couvrent réservations concurrentes, identité des personnages, blocages de déplacement/combat/voyage, PNJ remplacés/restaurés, libération des réservations, scores publics, invalidité des sièges et géométrie commune du corps et des équipements. Le formulaire et la fenêtre sont exercés en français, anglais et espagnol. Les transports de test ne remplacent pas la recette manuelle avec deux clients connectés. Les cérémonies royales finales, le décor complet, les tournois et les intégrations quêtes/achievements/classements restent à terminer.
