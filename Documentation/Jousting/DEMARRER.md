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

## Fenêtres automatiques dans le jeu

Il n’est plus nécessaire d’ouvrir Personnage ou le Lobby. Les fenêtres de joute utilisent l’interface native du jeu et s’ouvrent directement à l’arrivée sur les cases configurées de l’arène active :

- **Départ doré** : préparation de la joute, choix William / Tale Knight Night et difficulté, puis **Démarrer la joute IA**, ou **Défier un chevalier** pour le PvP.
- **Départ bleu** : fenêtre des duels ; le deuxième joueur peut y accepter l’invitation reçue. Le PvP doit être autorisé dans l’arène et les deux joueurs doivent être sur la même carte et dans la même instance, avec deux comptes distincts.
- **Accès d’un gradin** : fenêtre des places. L’ouverture ne réserve rien et n’assoit pas le joueur ; il choisit sa place et confirme.

Fermer une fenêtre la laisse fermée tant que le personnage reste sur cette case. Quitter la case puis revenir permet de la rouvrir. Les fenêtres des joutes se remplacent entre elles. Elles ne s’ouvrent pas au milieu d’une charge, d’une autre activité bloquante, d’un combat ou d’une cinématique. Le joueur conserve le contrôle de ses déplacements pendant la préparation ; la course ne démarre qu’après son choix. Les raccourcis du Personnage et du Lobby restent facultatifs.

L’arène doit être activée et enregistrée. La partie se lance dans le client du jeu, pas dans l’éditeur. Au lancement, le serveur attribue temporairement William au joueur : aucune possession ou sélection préalable de monture n’est nécessaire.

Une arène désactivée reste un brouillon enregistrable, mais ne propose aucune fenêtre en jeu. Le panneau de vérification et la zone permanente de diagnostic signalent maintenant explicitement cet état. Après activation, attendre la confirmation **Enregistré**, puis quitter et rejoindre à nouveau le départ doré. Les coordonnées sous la mini-carte permettent de retrouver la case exacte.

Le client et l’éditeur copient désormais les 20 images de joute et l’atlas `Jousting_Objects_16` dans leurs sorties de compilation et de publication. Les sous-dossiers William/Night sont conservés et les PNG restent externes aux exécutables. Une ancienne publication peut manquer de ces fichiers même si le client installé dans un autre dossier les possède. Le menu « Server Status: Offline » confirme seulement l’absence de connexion ; sa version est celle du client.

Le test GPU facultatif `RealJoustingMountDrawsProvidedWilliamAndSuppressesTheOrdinaryBody` utilise le chargeur de textures et le rendu de production avec les PNG locaux. Il vérifie que William atteint le framebuffer et que le corps ordinaire est masqué. Il requiert `CODEX_GPU_TESTS=1` et `CODEX_JOUST_RESOURCE_ROOT` pointant vers la racine qui contient `resources`. Ce contrôle ne remplace pas un duel effectué sur un serveur connecté.

## Ressources et vérification

Les feuilles Night fournies sont réutilisées directement sur la piste : cellules de 128 × 128, quatre images de repos et six images par phase de déplacement, comme William. Night est un adversaire éphémère ; il n’est pas ajouté aux montures possédées par le joueur. Le gros plan emploie la variante sombre déjà présente du sprite de prototype ; une animation frontale artistique propre à Night reste à dessiner. Aucun nouveau dessin n’est annoncé dans cette correction.

Les tests natifs couvrent les deux choix d’adversaire, leurs feuilles envoyées au client, la conservation de la monture du joueur, le cas exact d’accès éloigné, sa réparation, les messages visibles dans l’onglet Places et le démarrage activé au départ doré. Les interfaces sont vérifiées en français, anglais et espagnol. La recette graphique sur plusieurs clients connectés reste distincte de ces tests.
