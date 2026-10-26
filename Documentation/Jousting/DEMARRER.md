# Démarrer une joute — William et Tale Knight Night

## Le problème de la capture

La place `test` a son banc en `(20, 9)` et son accès en `(4, 9)`. Ces cases sont éloignées de seize cases ; un accès doit être directement adjacent au banc. Le bouton **Mettre l’accès à côté du banc** choisit une case libre voisine. On peut aussi placer l’accès manuellement en `(20, 10)`, `(20, 8)`, `(19, 9)` ou `(21, 9)`, si cette case est libre et hors des pistes.

Les erreurs sont maintenant affichées dans une zone permanente sous l’aperçu, quel que soit l’onglet ouvert. Le nom de la place et ses coordonnées figurent dans le message d’adjacence. Les refus connus du serveur indiquent aussi la ressource ou le réglage à corriger. Les contrôles des récompenses et des cartes utilisées par plusieurs arènes sont affichés dans la même zone.

## Préparation dans l’éditeur

1. Ouvrir la carte, puis **Mini-Games → Jousting Editor**.
2. La seule monture proposée pour les joueurs est **William**. Cliquer **Ajouter le préréglage William** si le panneau le demande. Les autres montures du jeu restent gérées par leur éditeur habituel.
3. Choisir l’adversaire IA par défaut : **William** ou **Tale Knight Night**. Ce choix peut être changé au lancement de l’entraînement dans le client.
4. Placer le départ doré, l’arrivée et le couloir bleu. Garder une distance horizontale paire, de 4 à 64 cases, et dégager les deux pistes. Cocher **Activer cette arène** et **Autoriser l’entraînement contre l’IA**.
5. Pour les spectateurs, placer un banc et un accès adjacent hors des pistes. Enregistrer l’arène. Les cinématiques sont facultatives pour lancer un premier essai.

## Lancer l’IA dans le client du jeu

1. Entrer sur la carte de l’arène avec son personnage. La partie se lance dans le client, pas dans l’éditeur.
2. Ouvrir **Personnage → Joutes Royales**, ou le bouton **Joutes Royales** toujours visible dans le **Lobby des aventures**. Il n’est pas nécessaire de créer un lobby.
3. La fenêtre indique les coordonnées du départ doré et la position actuelle du personnage. Marcher jusqu’à cette case : le bouton de démarrage devient disponible.
4. Choisir **William** ou **Tale Knight Night**, choisir la difficulté, puis cliquer **Démarrer la joute IA**. Le serveur attribue temporairement William au joueur ; il n’est pas nécessaire de posséder ou d’équiper cette monture au préalable.

## Lancer un duel entre joueurs

Deux comptes distincts doivent se trouver sur la même carte et dans la même instance, avec les duels autorisés dans l’arène. Le premier joueur se place au départ doré ; le second au départ bleu, à l’extrémité opposée. Ouvrir **Joutes Royales → Défier un chevalier**, choisir l’autre joueur et envoyer l’invitation. Le second dispose de trente secondes pour accepter. Les deux joueurs utilisent la monture de l’arène, William dans les arènes enregistrées par cet éditeur.

## Ressources et vérification

Les feuilles Night fournies sont réutilisées directement sur la piste : cellules de 128 × 128, quatre images de repos et six images par phase de déplacement, comme William. Night est un adversaire éphémère ; il n’est pas ajouté aux montures possédées par le joueur. Le gros plan emploie la variante sombre déjà présente du sprite de prototype ; une animation frontale artistique propre à Night reste à dessiner. Aucun nouveau dessin n’est annoncé dans cette correction.

Les tests natifs couvrent les deux choix d’adversaire, leurs feuilles envoyées au client, la conservation de la monture du joueur, le cas exact d’accès éloigné, sa réparation, les messages visibles dans l’onglet Places et le démarrage activé au départ doré. Les interfaces sont vérifiées en français, anglais et espagnol. La recette graphique sur plusieurs clients connectés reste distincte de ces tests.
